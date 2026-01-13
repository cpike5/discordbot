using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically cleans up old sound play logs according to the retention policy.
/// Deletes play log entries older than configured retention period.
/// </summary>
public class SoundPlayLogRetentionService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<SoundPlayLogRetentionOptions> _options;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;

    public override string ServiceName => "Sound Play Log Retention Service";

    /// <summary>
    /// Gets the service name formatted for tracing (snake_case).
    /// </summary>
    private string TracingServiceName => "sound_play_log_retention_service";

    /// <summary>
    /// Initializes a new instance of the <see cref="SoundPlayLogRetentionService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for health monitoring.</param>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="options">Sound play log retention configuration options.</param>
    /// <param name="bgOptions">Background services configuration options.</param>
    /// <param name="logger">The logger.</param>
    public SoundPlayLogRetentionService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<SoundPlayLogRetentionOptions> options,
        IOptions<BackgroundServicesOptions> bgOptions,
        ILogger<SoundPlayLogRetentionService> logger)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _bgOptions = bgOptions;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            _logger.LogInformation("Sound play log retention cleanup is disabled via configuration");
            return;
        }

        _logger.LogInformation("Sound play log retention service starting");

        _logger.LogInformation(
            "Sound play log retention enabled. Retention: {RetentionDays} days, Interval: {IntervalHours}h, Batch size: {BatchSize}",
            _options.Value.RetentionDays,
            _options.Value.CleanupIntervalHours,
            _options.Value.CleanupBatchSize);

        // Initial delay to let the app start up
        var initialDelay = TimeSpan.FromMinutes(_bgOptions.Value.AnalyticsAggregationInitialDelayMinutes);
        await Task.Delay(initialDelay, stoppingToken);

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
                var totalDeleted = await PerformCleanupAsync(stoppingToken);

                BotActivitySource.SetRecordsDeleted(activity, totalDeleted);
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
                _logger.LogError(ex, "Error during sound play log retention cleanup");
                RecordError(ex);
            }

            // Wait for next cleanup interval
            var interval = TimeSpan.FromHours(_options.Value.CleanupIntervalHours);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Sound play log retention service stopping");
    }

    /// <summary>
    /// Performs a single cleanup operation by removing old sound play logs.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during cleanup.</param>
    /// <returns>The total number of play log entries deleted.</returns>
    private async Task<int> PerformCleanupAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting sound play log retention cleanup");

        using var scope = _scopeFactory.CreateScope();
        var soundPlayLogRepo = scope.ServiceProvider.GetRequiredService<ISoundPlayLogRepository>();

        var totalDeleted = await CleanupPlayLogsAsync(soundPlayLogRepo, stoppingToken);

        _logger.LogInformation("Sound play log retention cleanup completed. Deleted {TotalCount} total play logs", totalDeleted);

        return totalDeleted;
    }

    /// <summary>
    /// Cleans up sound play logs older than the configured retention period.
    /// </summary>
    /// <param name="soundPlayLogRepo">The sound play log repository.</param>
    /// <param name="stoppingToken">Cancellation token.</param>
    /// <returns>Total number of play logs deleted.</returns>
    private async Task<int> CleanupPlayLogsAsync(
        ISoundPlayLogRepository soundPlayLogRepo,
        CancellationToken stoppingToken)
    {
        using var cleanupActivity = BotActivitySource.StartBackgroundCleanupActivity(
            TracingServiceName,
            "sound_play_logs");

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.Value.RetentionDays);
            var batchSize = _options.Value.CleanupBatchSize;
            var totalDeleted = 0;

            _logger.LogDebug("Cleaning up sound play logs older than {Cutoff}", cutoff);

            while (!stoppingToken.IsCancellationRequested)
            {
                var deleted = await soundPlayLogRepo.DeleteOlderThanAsync(cutoff, batchSize, stoppingToken);

                if (deleted == 0)
                {
                    break; // No more records to delete
                }

                totalDeleted += deleted;
                _logger.LogDebug("Deleted {Count} sound play logs (batch)", deleted);

                // If we deleted fewer than batch size, we're done
                if (deleted < batchSize)
                {
                    break;
                }

                // Brief delay between batches to avoid long-running transactions
                await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
            }

            if (totalDeleted > 0)
            {
                _logger.LogInformation("Deleted {Count} sound play logs older than {RetentionDays} days",
                    totalDeleted, _options.Value.RetentionDays);
            }

            BotActivitySource.SetRecordsDeleted(cleanupActivity, totalDeleted);
            BotActivitySource.SetSuccess(cleanupActivity);

            return totalDeleted;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(cleanupActivity, ex);
            throw;
        }
    }
}
