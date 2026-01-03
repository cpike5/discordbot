using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically checks for and delivers reminders that are due.
/// Runs at configured intervals and processes reminders concurrently with retry logic for failed deliveries.
/// </summary>
public class ReminderExecutionService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ReminderOptions> _options;
    private readonly DiscordSocketClient _client;

    public override string ServiceName => "Reminder Execution Service";

    /// <summary>
    /// Gets the tracing service name in snake_case format.
    /// </summary>
    private string TracingServiceName => "reminder_execution_service";

    /// <summary>
    /// Initializes a new instance of the <see cref="ReminderExecutionService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for health monitoring.</param>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="options">The reminder configuration options.</param>
    /// <param name="client">The Discord socket client for sending DMs.</param>
    /// <param name="logger">The logger.</param>
    public ReminderExecutionService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<ReminderOptions> options,
        DiscordSocketClient client,
        ILogger<ReminderExecutionService> logger)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _client = client;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reminder execution service starting");

        _logger.LogInformation(
            "Reminder execution service enabled. Check interval: {IntervalSeconds}s, Max concurrent: {MaxConcurrent}, Max attempts: {MaxAttempts}, Retry delay: {RetryMinutes}m",
            _options.Value.CheckIntervalSeconds,
            _options.Value.MaxConcurrentDeliveries,
            _options.Value.MaxDeliveryAttempts,
            _options.Value.RetryDelayMinutes);

        // Wait for Discord client to connect
        while (_client.ConnectionState != ConnectionState.Connected && !stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Waiting for Discord client to connect (current state: {State})", _client.ConnectionState);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        if (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Reminder execution service stopping before Discord connection established");
            return;
        }

        _logger.LogInformation("Discord client connected, reminder execution service ready");

        var executionCycle = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            executionCycle++;
            var correlationId = Guid.NewGuid().ToString("N")[..16];

            using var activity = BotActivitySource.StartBackgroundServiceActivity(
                TracingServiceName,
                executionCycle,
                correlationId);

            UpdateHeartbeat();

            try
            {
                var remindersProcessed = await ProcessDueRemindersAsync(stoppingToken);

                BotActivitySource.SetRecordsProcessed(activity, remindersProcessed);
                BotActivitySource.SetSuccess(activity);
                ClearError();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reminder processing");
                BotActivitySource.RecordException(activity, ex);
                RecordError(ex);
            }

            // Wait for next check interval
            var interval = TimeSpan.FromSeconds(_options.Value.CheckIntervalSeconds);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Reminder execution service stopping");
    }

    /// <summary>
    /// Processes all due reminders by delivering them concurrently with retry logic.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during processing.</param>
    /// <returns>The number of reminders processed.</returns>
    private async Task<int> ProcessDueRemindersAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Checking for due reminders");

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReminderRepository>();

        // Get all due reminders
        var dueReminders = await repository.GetDueRemindersAsync(stoppingToken);
        var reminderList = dueReminders.ToList();

        if (reminderList.Count == 0)
        {
            _logger.LogTrace("No reminders due for delivery");
            return 0;
        }

        _logger.LogInformation("Found {Count} reminders due for delivery", reminderList.Count);

        using var batchActivity = BotActivitySource.StartBackgroundBatchActivity(
            TracingServiceName,
            reminderList.Count,
            "reminders");

        try
        {
            // Create a semaphore to limit concurrent deliveries
            using var semaphore = new SemaphoreSlim(_options.Value.MaxConcurrentDeliveries);

            // Deliver reminders concurrently with semaphore protection
            var deliveryTasks = reminderList.Select(async reminder =>
            {
                await semaphore.WaitAsync(stoppingToken);
                try
                {
                    _logger.LogDebug("Delivering reminder {ReminderId} to user {UserId}",
                        reminder.Id, reminder.UserId);

                    await DeliverReminderAsync(reminder, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Reminder delivery cancelled due to shutdown: {ReminderId}",
                        reminder.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error delivering reminder {ReminderId} to user {UserId}",
                        reminder.Id, reminder.UserId);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            // Wait for all deliveries to complete
            await Task.WhenAll(deliveryTasks);

            _logger.LogInformation("Completed processing {Count} due reminders", reminderList.Count);

            BotActivitySource.SetRecordsProcessed(batchActivity, reminderList.Count);
            BotActivitySource.SetSuccess(batchActivity);

            return reminderList.Count;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(batchActivity, ex);
            throw;
        }
    }

    /// <summary>
    /// Delivers a single reminder to the user via DM with retry logic.
    /// </summary>
    /// <param name="reminder">The reminder to deliver.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task DeliverReminderAsync(Core.Entities.Reminder reminder, CancellationToken ct)
    {
        try
        {
            // Get the user
            var user = _client.GetUser(reminder.UserId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for reminder {ReminderId}, marking as failed",
                    reminder.UserId, reminder.Id);
                await UpdateReminderFailedAsync(reminder.Id, "User not found", ct);
                return;
            }

            // Build the embed
            var embed = new EmbedBuilder()
                .WithTitle("⏰ Reminder")
                .WithDescription(reminder.Message)
                .WithColor(Color.Blue)
                .AddField("Set", $"<t:{((DateTimeOffset)reminder.CreatedAt).ToUnixTimeSeconds()}:R>", inline: true)
                .AddField("Context", $"[Jump to channel](https://discord.com/channels/{reminder.GuildId}/{reminder.ChannelId})", inline: true)
                .WithFooter($"Reminder ID: {reminder.Id}")
                .WithCurrentTimestamp()
                .Build();

            // Try to send the DM
            try
            {
                await user.SendMessageAsync(embed: embed);

                // Mark as delivered
                await UpdateReminderDeliveredAsync(reminder.Id, ct);

                _logger.LogInformation("Successfully delivered reminder {ReminderId} to user {UserId}",
                    reminder.Id, reminder.UserId);
            }
            catch (Discord.Net.HttpException ex) when (ex.DiscordCode == Discord.DiscordErrorCode.CannotSendMessageToUser)
            {
                // User has DMs disabled - handle retry logic
                _logger.LogWarning("Failed to deliver reminder {ReminderId} to user {UserId}: DMs disabled (attempt {Attempt}/{MaxAttempts})",
                    reminder.Id, reminder.UserId, reminder.DeliveryAttempts + 1, _options.Value.MaxDeliveryAttempts);

                await HandleDeliveryFailureAsync(reminder, "User has DMs disabled", ct);
            }
            catch (Exception ex)
            {
                // Other delivery failure
                _logger.LogError(ex, "Failed to deliver reminder {ReminderId} to user {UserId} (attempt {Attempt}/{MaxAttempts})",
                    reminder.Id, reminder.UserId, reminder.DeliveryAttempts + 1, _options.Value.MaxDeliveryAttempts);

                await HandleDeliveryFailureAsync(reminder, ex.Message, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing reminder {ReminderId}", reminder.Id);
        }
    }

    /// <summary>
    /// Handles delivery failure by incrementing attempts and scheduling retry or marking as failed.
    /// </summary>
    /// <param name="reminder">The reminder that failed delivery.</param>
    /// <param name="errorMessage">The error message to record.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task HandleDeliveryFailureAsync(Core.Entities.Reminder reminder, string errorMessage, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReminderRepository>();

        var newAttemptCount = reminder.DeliveryAttempts + 1;

        if (newAttemptCount >= _options.Value.MaxDeliveryAttempts)
        {
            // Max attempts reached - mark as failed
            reminder.Status = ReminderStatus.Failed;
            reminder.DeliveryAttempts = newAttemptCount;
            reminder.LastError = errorMessage;

            await repository.UpdateAsync(reminder, ct);

            _logger.LogWarning("Reminder {ReminderId} marked as failed after {Attempts} attempts: {Error}",
                reminder.Id, newAttemptCount, errorMessage);
        }
        else
        {
            // Schedule retry
            reminder.DeliveryAttempts = newAttemptCount;
            reminder.LastError = errorMessage;
            reminder.TriggerAt = DateTime.UtcNow.AddMinutes(_options.Value.RetryDelayMinutes);

            await repository.UpdateAsync(reminder, ct);

            _logger.LogInformation("Reminder {ReminderId} scheduled for retry at {RetryAt} (attempt {Attempt}/{MaxAttempts})",
                reminder.Id, reminder.TriggerAt, newAttemptCount, _options.Value.MaxDeliveryAttempts);
        }
    }

    /// <summary>
    /// Marks a reminder as delivered in the database.
    /// </summary>
    /// <param name="reminderId">The reminder ID.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task UpdateReminderDeliveredAsync(Guid reminderId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReminderRepository>();

        var reminder = await repository.GetByIdAsync(reminderId, ct);
        if (reminder == null)
        {
            _logger.LogWarning("Reminder {ReminderId} not found for delivery status update", reminderId);
            return;
        }

        reminder.Status = ReminderStatus.Delivered;
        reminder.DeliveredAt = DateTime.UtcNow;

        await repository.UpdateAsync(reminder, ct);
    }

    /// <summary>
    /// Marks a reminder as failed in the database.
    /// </summary>
    /// <param name="reminderId">The reminder ID.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task UpdateReminderFailedAsync(Guid reminderId, string errorMessage, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReminderRepository>();

        var reminder = await repository.GetByIdAsync(reminderId, ct);
        if (reminder == null)
        {
            _logger.LogWarning("Reminder {ReminderId} not found for failed status update", reminderId);
            return;
        }

        reminder.Status = ReminderStatus.Failed;
        reminder.LastError = errorMessage;

        await repository.UpdateAsync(reminder, ct);
    }
}
