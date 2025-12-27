using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically checks for and executes scheduled messages that are due.
/// Runs at configured intervals and processes messages concurrently with timeout protection.
/// </summary>
public class ScheduledMessageExecutionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<ScheduledMessagesOptions> _options;
    private readonly ILogger<ScheduledMessageExecutionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduledMessageExecutionService"/> class.
    /// </summary>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="options">The scheduled messages configuration options.</param>
    /// <param name="logger">The logger.</param>
    public ScheduledMessageExecutionService(
        IServiceScopeFactory scopeFactory,
        IOptions<ScheduledMessagesOptions> options,
        ILogger<ScheduledMessageExecutionService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduled message execution service starting");

        _logger.LogInformation(
            "Scheduled message execution service enabled. Check interval: {IntervalSeconds}s, Max concurrent: {MaxConcurrent}, Timeout: {TimeoutSeconds}s",
            _options.Value.CheckIntervalSeconds,
            _options.Value.MaxConcurrentExecutions,
            _options.Value.ExecutionTimeoutSeconds);

        // Initial delay to let the app start up and Discord client connect
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled message processing");
            }

            // Wait for next check interval
            var interval = TimeSpan.FromSeconds(_options.Value.CheckIntervalSeconds);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Scheduled message execution service stopping");
    }

    /// <summary>
    /// Processes all due scheduled messages by executing them concurrently with timeout protection.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during processing.</param>
    private async Task ProcessDueMessagesAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Checking for due scheduled messages");

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IScheduledMessageRepository>();
        var service = scope.ServiceProvider.GetRequiredService<IScheduledMessageService>();

        // Get all due messages
        var dueMessages = await repository.GetDueMessagesAsync(stoppingToken);
        var messageList = dueMessages.ToList();

        if (messageList.Count == 0)
        {
            _logger.LogTrace("No scheduled messages due for execution");
            return;
        }

        _logger.LogInformation("Found {Count} scheduled messages due for execution", messageList.Count);

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

                _logger.LogDebug("Executing scheduled message {MessageId}: {Title}",
                    message.Id, message.Title);

                var success = await service.ExecuteScheduledMessageAsync(message.Id, cts.Token);

                if (success)
                {
                    _logger.LogInformation("Successfully executed scheduled message {MessageId}: {Title}",
                        message.Id, message.Title);
                }
                else
                {
                    _logger.LogWarning("Failed to execute scheduled message {MessageId}: {Title}",
                        message.Id, message.Title);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Scheduled message execution cancelled due to shutdown: {MessageId}",
                    message.Id);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Scheduled message execution timed out after {Timeout}s: {MessageId}",
                    executionTimeout.TotalSeconds, message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing scheduled message {MessageId}: {Title}",
                    message.Id, message.Title);
            }
            finally
            {
                semaphore.Release();
            }
        });

        // Wait for all executions to complete
        await Task.WhenAll(executionTasks);

        _logger.LogInformation("Completed processing {Count} scheduled messages", messageList.Count);
    }
}
