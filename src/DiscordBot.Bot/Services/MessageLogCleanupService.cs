using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically cleans up old message logs according to the retention policy.
/// Respects the configured retention period and runs cleanup at scheduled intervals.
/// </summary>
public class MessageLogCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<MessageLogRetentionOptions> _options;
    private readonly ILogger<MessageLogCleanupService> _logger;

    public MessageLogCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<MessageLogRetentionOptions> options,
        ILogger<MessageLogCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message log cleanup service starting");

        // Check if cleanup is enabled
        if (!_options.Value.Enabled)
        {
            _logger.LogInformation("Message log cleanup is disabled via configuration");
            return;
        }

        _logger.LogInformation(
            "Message log cleanup service enabled. Retention: {RetentionDays} days, Interval: {IntervalHours} hours, Batch size: {BatchSize}",
            _options.Value.RetentionDays,
            _options.Value.CleanupIntervalHours,
            _options.Value.CleanupBatchSize);

        // Initial delay (5 minutes) to let the app start up
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during message log cleanup");
            }

            // Wait for next cleanup interval
            var interval = TimeSpan.FromHours(_options.Value.CleanupIntervalHours);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Message log cleanup service stopping");
    }

    /// <summary>
    /// Performs a single cleanup operation by creating a scoped service and invoking the cleanup method.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during cleanup.</param>
    private async Task PerformCleanupAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting message log cleanup. Retention: {RetentionDays} days, Batch size: {BatchSize}",
            _options.Value.RetentionDays,
            _options.Value.CleanupBatchSize);

        using var scope = _scopeFactory.CreateScope();
        var messageLogService = scope.ServiceProvider.GetRequiredService<IMessageLogService>();

        var deletedCount = await messageLogService.CleanupOldMessagesAsync(stoppingToken);

        _logger.LogInformation(
            "Message log cleanup completed. Deleted {DeletedCount} messages older than {RetentionDays} days",
            deletedCount,
            _options.Value.RetentionDays);
    }
}
