using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.RatWatch;

/// <summary>
/// Background service that periodically checks for due Rat Watches and expired voting sessions.
/// Runs at configured intervals and processes watches concurrently with timeout protection.
/// </summary>
public class RatWatchExecutionService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<RatWatchOptions> _options;
    private readonly DiscordSocketClient _client;
    private readonly IRatWatchStatusService _ratWatchStatusService;
    private readonly IDashboardUpdateService _dashboardUpdateService;

    public override string ServiceName => "RatWatchExecutionService";

    /// <summary>
    /// Gets the service name in snake_case format for tracing.
    /// </summary>
    protected virtual string TracingServiceName =>
        ServiceName.ToLowerInvariant().Replace(" ", "_");

    /// <summary>
    /// Initializes a new instance of the <see cref="RatWatchExecutionService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="options">The Rat Watch configuration options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="client">The Discord socket client for posting messages.</param>
    /// <param name="ratWatchStatusService">The Rat Watch status service for bot status updates.</param>
    /// <param name="dashboardUpdateService">The dashboard update service for broadcasting activity.</param>
    /// <param name="serviceProvider">The service provider for MonitoredBackgroundService.</param>
    public RatWatchExecutionService(
        IServiceScopeFactory scopeFactory,
        IOptions<RatWatchOptions> options,
        ILogger<RatWatchExecutionService> logger,
        DiscordSocketClient client,
        IRatWatchStatusService ratWatchStatusService,
        IDashboardUpdateService dashboardUpdateService,
        IServiceProvider serviceProvider)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _client = client;
        _ratWatchStatusService = ratWatchStatusService;
        _dashboardUpdateService = dashboardUpdateService;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        // Initial delay to let the app start up and Discord client connect
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

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
                // Process both due watches and expired voting in parallel
                var dueWatchesTask = ProcessDueWatchesAsync(stoppingToken);
                var expiredVotingTask = ProcessExpiredVotingAsync(stoppingToken);

                await Task.WhenAll(dueWatchesTask, expiredVotingTask);

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
                BotActivitySource.RecordException(activity, ex);
                RecordError(ex);
            }

            // Wait for next check interval
            var interval = TimeSpan.FromSeconds(_options.Value.CheckIntervalSeconds);
            await Task.Delay(interval, stoppingToken);
        }
    }

    /// <summary>
    /// Processes all due Rat Watches by posting voting messages and starting the voting process.
    /// Skips watches that are more than 5 minutes past their scheduled time (expired).
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during processing.</param>
    private async Task ProcessDueWatchesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRatWatchService>();

        // Get all due watches
        var dueWatches = await service.GetDueWatchesAsync(stoppingToken);
        var watchList = dueWatches.ToList();

        if (watchList.Count == 0)
        {
            return;
        }

        using var batchActivity = BotActivitySource.StartBackgroundBatchActivity(
            TracingServiceName,
            watchList.Count,
            "due_watches");

        try
        {
            // Create a semaphore to limit concurrent executions
            using var semaphore = new SemaphoreSlim(_options.Value.MaxConcurrentExecutions);
            var executionTimeout = TimeSpan.FromSeconds(_options.Value.ExecutionTimeoutSeconds);
            var expirationThreshold = TimeSpan.FromMinutes(5);
            var processedCount = 0;

            // Execute watches concurrently with semaphore and timeout protection
            var executionTasks = watchList.Select(async watch =>
            {
                await semaphore.WaitAsync(stoppingToken);
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    cts.CancelAfter(executionTimeout);

                    var now = DateTime.UtcNow;
                    var timeSinceScheduled = now - watch.ScheduledAt;

                    // Skip if expired (more than 5 minutes past scheduled time)
                    if (timeSinceScheduled > expirationThreshold)
                    {
                        return false;
                    }

                    // Re-check status before posting (handles race condition with cancellation)
                    var currentWatch = await service.GetByIdAsync(watch.Id, cts.Token);
                    if (currentWatch == null || currentWatch.Status != RatWatchStatus.Pending)
                    {
                        return false;
                    }

                    // Post the voting message to Discord
                    var success = await PostVotingMessageAsync(
                        watch.Id,
                        watch.GuildId,
                        watch.ChannelId,
                        watch.AccusedUserId,
                        watch.OriginalMessageId,
                        watch.CustomMessage,
                        cts.Token);

                    return success;
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Timeout
                    return false;
                }
                catch (Exception)
                {
                    // Logged by individual methods
                    return false;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            // Wait for all executions to complete and count successes
            var results = await Task.WhenAll(executionTasks);
            processedCount = results.Count(r => r);

            BotActivitySource.SetRecordsProcessed(batchActivity, processedCount);
            BotActivitySource.SetSuccess(batchActivity);

            // Notify that voting has started for one or more watches - update bot status
            _ratWatchStatusService.RequestStatusUpdate();
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(batchActivity, ex);
            throw;
        }
    }

    /// <summary>
    /// Processes all Rat Watches with expired voting by finalizing the vote and updating Discord messages.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during processing.</param>
    private async Task ProcessExpiredVotingAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRatWatchService>();

        // Get all watches with expired voting
        var expiredVoting = await service.GetExpiredVotingAsync(stoppingToken);
        var votingList = expiredVoting.ToList();

        if (votingList.Count == 0)
        {
            return;
        }

        using var batchActivity = BotActivitySource.StartBackgroundBatchActivity(
            TracingServiceName,
            votingList.Count,
            "expired_voting");

        try
        {
            // Create a semaphore to limit concurrent executions
            using var semaphore = new SemaphoreSlim(_options.Value.MaxConcurrentExecutions);
            var executionTimeout = TimeSpan.FromSeconds(_options.Value.ExecutionTimeoutSeconds);
            var processedCount = 0;

            // Execute voting finalization concurrently with semaphore and timeout protection
            var executionTasks = votingList.Select(async watch =>
            {
                await semaphore.WaitAsync(stoppingToken);
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    cts.CancelAfter(executionTimeout);

                    // Finalize the voting in the database
                    var success = await service.FinalizeVotingAsync(watch.Id, cts.Token);

                    if (success)
                    {
                        // Update the Discord message with final results
                        await UpdateVotingMessageAsync(watch.Id, cts.Token);
                        return true;
                    }

                    return false;
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Timeout
                    return false;
                }
                catch (Exception)
                {
                    // Logged by individual methods
                    return false;
                }
                finally
                {
                    semaphore.Release();
                }
            });

            // Wait for all executions to complete and count successes
            var results = await Task.WhenAll(executionTasks);
            processedCount = results.Count(r => r);

            BotActivitySource.SetRecordsProcessed(batchActivity, processedCount);
            BotActivitySource.SetSuccess(batchActivity);

            // Notify that voting has ended for one or more watches - update bot status
            _ratWatchStatusService.RequestStatusUpdate();
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(batchActivity, ex);
            throw;
        }
    }

    /// <summary>
    /// Posts the voting message to Discord with voting buttons.
    /// </summary>
    /// <param name="watchId">The Rat Watch ID.</param>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="channelId">The Discord channel ID.</param>
    /// <param name="accusedUserId">The accused user's Discord ID.</param>
    /// <param name="originalMessageId">The original message ID.</param>
    /// <param name="customMessage">Optional custom message.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if successful, false otherwise.</returns>
    private async Task<bool> PostVotingMessageAsync(
        Guid watchId,
        ulong guildId,
        ulong channelId,
        ulong accusedUserId,
        ulong originalMessageId,
        string? customMessage,
        CancellationToken ct)
    {
        try
        {
            var channel = _client.GetChannel(channelId) as IMessageChannel;
            if (channel == null)
            {
                return false;
            }

            // Build the message content
            var messageLink = $"https://discord.com/channels/{guildId}/{channelId}/{originalMessageId}";
            var mention = $"<@{accusedUserId}>";

            var messageContent = $"🐀 **Rat check!** {mention}";
            if (!string.IsNullOrWhiteSpace(customMessage))
            {
                messageContent += $"\n> {customMessage}";
            }
            messageContent += $"\n[Jump to original message]({messageLink})";

            // Build the voting buttons
            var components = new ComponentBuilder()
                .WithButton("Rat 🐀", ComponentIdBuilder.Build("ratwatch", "vote", accusedUserId, watchId.ToString(), "guilty"), ButtonStyle.Danger)
                .WithButton("Not Rat ✓", ComponentIdBuilder.Build("ratwatch", "vote", accusedUserId, watchId.ToString(), "notguilty"), ButtonStyle.Success)
                .Build();

            // Post the message
            var message = await channel.SendMessageAsync(messageContent, components: components);

            // Start the voting process and set the voting message ID in a single operation
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRatWatchService>();

            await service.StartVotingAsync(watchId, message.Id, ct);

            // Broadcast Rat Watch voting started event to dashboard
            var guild = _client.GetGuild(guildId);
            var guildName = guild?.Name ?? "Unknown";
            var user = guild?.GetUser(accusedUserId);
            var username = user?.Username ?? "Unknown";
            await _dashboardUpdateService.BroadcastRatWatchActivityAsync(
                guildId,
                guildName,
                "RatWatchVotingStarted",
                username,
                cancellationToken: ct);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Updates the voting message with final results and disables the voting buttons.
    /// </summary>
    /// <param name="watchId">The Rat Watch ID.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task UpdateVotingMessageAsync(Guid watchId, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRatWatchService>();

            var watchDto = await service.GetByIdAsync(watchId, ct);
            if (watchDto == null)
            {
                return;
            }

            var channel = _client.GetChannel(watchDto.ChannelId) as IMessageChannel;
            if (channel == null)
            {
                return;
            }

            // Try to get the voting message
            if (!watchDto.VotingMessageId.HasValue)
            {
                return;
            }

            var message = await channel.GetMessageAsync(watchDto.VotingMessageId.Value);
            if (message is not IUserMessage userMessage)
            {
                return;
            }

            // Build the verdict message
            var isGuilty = watchDto.Status == RatWatchStatus.Guilty;
            var verdictEmoji = isGuilty ? "🐀" : "✅";
            var verdictText = isGuilty ? "**GUILTY**" : "**CLEARED**";

            var updatedContent = $"{verdictEmoji} {verdictText} — {watchDto.GuiltyVotes} Rat, {watchDto.NotGuiltyVotes} Not Rat";

            // Disable the buttons
            var disabledComponents = new ComponentBuilder()
                .WithButton("Rat 🐀", "disabled_guilty", ButtonStyle.Danger, disabled: true)
                .WithButton("Not Rat ✓", "disabled_notguilty", ButtonStyle.Success, disabled: true)
                .Build();

            // Update the message
            await userMessage.ModifyAsync(props =>
            {
                props.Content = updatedContent;
                props.Components = disabledComponents;
            });

            // Broadcast Rat Watch voting ended event to dashboard
            var guild = _client.GetGuild(watchDto.GuildId);
            var guildName = guild?.Name ?? "Unknown";
            var verdict = isGuilty ? "Guilty" : "Not Guilty";
            await _dashboardUpdateService.BroadcastRatWatchActivityAsync(
                watchDto.GuildId,
                guildName,
                "RatWatchVotingEnded",
                watchDto.AccusedUsername,
                verdict,
                ct);
        }
        catch (Exception)
        {
            // Errors logged elsewhere
        }
    }
}
