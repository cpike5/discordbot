using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically checks for and executes scheduled messages that are due.
/// Runs at configured intervals and processes messages concurrently with timeout protection.
/// </summary>
public class ScheduledMessageExecutionService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ScheduledMessagesOptions> _options;

    public override string ServiceName => "ScheduledMessageExecutionService";

    /// <summary>
    /// Gets the tracing service name in snake_case format.
    /// </summary>
    private string TracingServiceName => "scheduled_message_execution_service";

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledMessageExecutionService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="options">The scheduled messages configuration options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider for MonitoredBackgroundService.</param>
    public ScheduledMessageExecutionService(
        IServiceScopeFactory scopeFactory,
        IOptions<ScheduledMessagesOptions> options,
        ILogger<ScheduledMessageExecutionService> logger,
        IServiceProvider serviceProvider)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        // Initial delay to let the app start up and Discord client connect
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

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
                var messagesProcessed = await ProcessDueMessagesAsync(stoppingToken);

                BotActivitySource.SetRecordsProcessed(activity, messagesProcessed);
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
    /// Processes all due scheduled messages by executing them concurrently with timeout protection.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during processing.</param>
    /// <returns>The number of messages processed.</returns>
    private async Task<int> ProcessDueMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IScheduledMessageRepository>();
        var service = scope.ServiceProvider.GetRequiredService<IScheduledMessageService>();

        // Get all due messages
        var dueMessages = await repository.GetDueMessagesAsync(stoppingToken);
        var messageList = dueMessages.ToList();

        if (messageList.Count == 0)
        {
            return 0;
        }

        using var batchActivity = BotActivitySource.StartBackgroundBatchActivity(
            TracingServiceName,
            messageList.Count,
            "scheduled_messages");

        try
        {
            // Create a semaphore to limit concurrent executions
            using var semaphore = new SemaphoreSlim(_options.Value.MaxConcurrentExecutions);
            var executionTimeout = TimeSpan.FromSeconds(_options.Value.ExecutionTimeoutSeconds);

            // Execute messages concurrently with semaphore and timeout protection
            var executionTasks = messageList.Select(async message =>
            {
                await semaphore.WaitAsync(stoppingToken);
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    cts.CancelAfter(executionTimeout);

                    await service.ExecuteScheduledMessageAsync(message.Id, cts.Token);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Timeout
                }
                catch (Exception)
                {
                    // Logged by service
                }
                finally
                {
                    semaphore.Release();
                }
            });

            // Wait for all executions to complete
            await Task.WhenAll(executionTasks);

            BotActivitySource.SetRecordsProcessed(batchActivity, messageList.Count);
            BotActivitySource.SetSuccess(batchActivity);

            return messageList.Count;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(batchActivity, ex);
            throw;
        }
    }
}
