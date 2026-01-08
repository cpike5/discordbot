using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically cleans up old analytics snapshots according to the retention policy.
/// Deletes hourly snapshots older than configured retention period and daily snapshots beyond their retention.
/// </summary>
public class AnalyticsRetentionService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AnalyticsRetentionOptions> _options;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;

    public override string ServiceName => "Analytics Retention Service";

    /// <summary>
    /// Gets the service name formatted for tracing (snake_case).
    /// </summary>
    private string TracingServiceName => "analytics_retention_service";

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsRetentionService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for health monitoring.</param>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="options">Analytics retention configuration options.</param>
    /// <param name="bgOptions">Background services configuration options.</param>
    /// <param name="logger">The logger.</param>
    public AnalyticsRetentionService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<AnalyticsRetentionOptions> options,
        IOptions<BackgroundServicesOptions> bgOptions,
        ILogger<AnalyticsRetentionService> logger)
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
            _logger.LogInformation("Analytics retention cleanup is disabled via configuration");
            return;
        }

        _logger.LogInformation("Analytics retention service starting");

        _logger.LogInformation(
            "Analytics retention enabled. Hourly: {HourlyDays}d, Daily: {DailyDays}d, Events: {EventDays}d, Interval: {IntervalHours}h, Batch: {BatchSize}",
            _options.Value.HourlyRetentionDays,
            _options.Value.DailyRetentionDays,
            _options.Value.ActivityEventRetentionDays,
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
                _logger.LogError(ex, "Error during analytics retention cleanup");
                RecordError(ex);
            }

            // Wait for next cleanup interval
            var interval = TimeSpan.FromHours(_options.Value.CleanupIntervalHours);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Analytics retention service stopping");
    }

    /// <summary>
    /// Performs a single cleanup operation by removing old snapshots across all repositories.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during cleanup.</param>
    /// <returns>The total number of analytics records deleted.</returns>
    private async Task<int> PerformCleanupAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting analytics retention cleanup");

        using var scope = _scopeFactory.CreateScope();
        var memberActivityRepo = scope.ServiceProvider.GetRequiredService<IMemberActivityRepository>();
        var channelActivityRepo = scope.ServiceProvider.GetRequiredService<IChannelActivityRepository>();
        var guildMetricsRepo = scope.ServiceProvider.GetRequiredService<IGuildMetricsRepository>();
        var activityEventRepo = scope.ServiceProvider.GetRequiredService<IUserActivityEventRepository>();

        var totalDeleted = 0;

        // Clean up hourly member activity snapshots
        var hourlyMemberDeleted = await CleanupHourlySnapshotsAsync(
            "member activity",
            async (cutoff, batchSize, ct) => await memberActivityRepo.DeleteOlderThanAsync(cutoff, SnapshotGranularity.Hourly, batchSize, ct),
            stoppingToken);
        totalDeleted += hourlyMemberDeleted;

        // Clean up hourly channel activity snapshots
        var hourlyChannelDeleted = await CleanupHourlySnapshotsAsync(
            "channel activity",
            async (cutoff, batchSize, ct) => await channelActivityRepo.DeleteOlderThanAsync(cutoff, SnapshotGranularity.Hourly, batchSize, ct),
            stoppingToken);
        totalDeleted += hourlyChannelDeleted;

        // Clean up daily member activity snapshots
        var dailyMemberDeleted = await CleanupDailySnapshotsAsync(
            "member activity",
            async (cutoff, batchSize, ct) => await memberActivityRepo.DeleteOlderThanAsync(cutoff, SnapshotGranularity.Daily, batchSize, ct),
            stoppingToken);
        totalDeleted += dailyMemberDeleted;

        // Clean up daily channel activity snapshots
        var dailyChannelDeleted = await CleanupDailySnapshotsAsync(
            "channel activity",
            async (cutoff, batchSize, ct) => await channelActivityRepo.DeleteOlderThanAsync(cutoff, SnapshotGranularity.Daily, batchSize, ct),
            stoppingToken);
        totalDeleted += dailyChannelDeleted;

        // Clean up daily guild metrics snapshots
        var guildMetricsDeleted = await CleanupGuildMetricsSnapshotsAsync(guildMetricsRepo, stoppingToken);
        totalDeleted += guildMetricsDeleted;

        // Clean up raw UserActivityEvent records (after aggregation window)
        var activityEventsDeleted = await CleanupActivityEventsAsync(activityEventRepo, stoppingToken);
        totalDeleted += activityEventsDeleted;

        _logger.LogInformation("Analytics retention cleanup completed. Deleted {TotalCount} total records", totalDeleted);

        return totalDeleted;
    }

    /// <summary>
    /// Cleans up hourly snapshots older than the configured retention period.
    /// </summary>
    /// <param name="snapshotType">The type of snapshot for logging (e.g., "member activity").</param>
    /// <param name="deleteFunc">The delete function to execute.</param>
    /// <param name="stoppingToken">Cancellation token.</param>
    /// <returns>Total number of snapshots deleted.</returns>
    private async Task<int> CleanupHourlySnapshotsAsync(
        string snapshotType,
        Func<DateTime, int, CancellationToken, Task<int>> deleteFunc,
        CancellationToken stoppingToken)
    {
        var targetType = $"hourly_{snapshotType.Replace(" ", "_")}";
        using var cleanupActivity = BotActivitySource.StartBackgroundCleanupActivity(
            TracingServiceName,
            targetType);

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.Value.HourlyRetentionDays);
            var batchSize = _options.Value.CleanupBatchSize;
            var totalDeleted = 0;

            _logger.LogDebug("Cleaning up hourly {SnapshotType} snapshots older than {Cutoff}", snapshotType, cutoff);

            while (!stoppingToken.IsCancellationRequested)
            {
                var deleted = await deleteFunc(cutoff, batchSize, stoppingToken);

                if (deleted == 0)
                {
                    break; // No more records to delete
                }

                totalDeleted += deleted;
                _logger.LogDebug("Deleted {Count} hourly {SnapshotType} snapshots (batch)", deleted, snapshotType);

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
                _logger.LogInformation("Deleted {Count} hourly {SnapshotType} snapshots older than {RetentionDays} days",
                    totalDeleted, snapshotType, _options.Value.HourlyRetentionDays);
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

    /// <summary>
    /// Cleans up daily snapshots older than the configured retention period.
    /// </summary>
    /// <param name="snapshotType">The type of snapshot for logging (e.g., "member activity").</param>
    /// <param name="deleteFunc">The delete function to execute.</param>
    /// <param name="stoppingToken">Cancellation token.</param>
    /// <returns>Total number of snapshots deleted.</returns>
    private async Task<int> CleanupDailySnapshotsAsync(
        string snapshotType,
        Func<DateTime, int, CancellationToken, Task<int>> deleteFunc,
        CancellationToken stoppingToken)
    {
        var targetType = $"daily_{snapshotType.Replace(" ", "_")}";
        using var cleanupActivity = BotActivitySource.StartBackgroundCleanupActivity(
            TracingServiceName,
            targetType);

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.Value.DailyRetentionDays);
            var batchSize = _options.Value.CleanupBatchSize;
            var totalDeleted = 0;

            _logger.LogDebug("Cleaning up daily {SnapshotType} snapshots older than {Cutoff}", snapshotType, cutoff);

            while (!stoppingToken.IsCancellationRequested)
            {
                var deleted = await deleteFunc(cutoff, batchSize, stoppingToken);

                if (deleted == 0)
                {
                    break; // No more records to delete
                }

                totalDeleted += deleted;
                _logger.LogDebug("Deleted {Count} daily {SnapshotType} snapshots (batch)", deleted, snapshotType);

                // If we deleted fewer than batch size, we're done
                if (deleted < batchSize)
                {
                    break;
                }

                // Brief delay between batches
                await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
            }

            if (totalDeleted > 0)
            {
                _logger.LogInformation("Deleted {Count} daily {SnapshotType} snapshots older than {RetentionDays} days",
                    totalDeleted, snapshotType, _options.Value.DailyRetentionDays);
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

    /// <summary>
    /// Cleans up guild metrics snapshots older than the configured retention period.
    /// </summary>
    /// <param name="guildMetricsRepo">The guild metrics repository.</param>
    /// <param name="stoppingToken">Cancellation token.</param>
    /// <returns>Total number of snapshots deleted.</returns>
    private async Task<int> CleanupGuildMetricsSnapshotsAsync(
        IGuildMetricsRepository guildMetricsRepo,
        CancellationToken stoppingToken)
    {
        using var cleanupActivity = BotActivitySource.StartBackgroundCleanupActivity(
            TracingServiceName,
            "guild_metrics");

        try
        {
            var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-_options.Value.DailyRetentionDays));
            var batchSize = _options.Value.CleanupBatchSize;
            var totalDeleted = 0;

            _logger.LogDebug("Cleaning up guild metrics snapshots older than {Cutoff}", cutoffDate);

            while (!stoppingToken.IsCancellationRequested)
            {
                var deleted = await guildMetricsRepo.DeleteOlderThanAsync(cutoffDate, batchSize, stoppingToken);

                if (deleted == 0)
                {
                    break; // No more records to delete
                }

                totalDeleted += deleted;
                _logger.LogDebug("Deleted {Count} guild metrics snapshots (batch)", deleted);

                // If we deleted fewer than batch size, we're done
                if (deleted < batchSize)
                {
                    break;
                }

                // Brief delay between batches
                await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
            }

            if (totalDeleted > 0)
            {
                _logger.LogInformation("Deleted {Count} guild metrics snapshots older than {RetentionDays} days",
                    totalDeleted, _options.Value.DailyRetentionDays);
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

    /// <summary>
    /// Cleans up raw UserActivityEvent records older than the configured retention period.
    /// These events should be aggregated into snapshots before deletion.
    /// </summary>
    /// <param name="activityEventRepo">The activity event repository.</param>
    /// <param name="stoppingToken">Cancellation token.</param>
    /// <returns>Total number of activity events deleted.</returns>
    private async Task<int> CleanupActivityEventsAsync(
        IUserActivityEventRepository activityEventRepo,
        CancellationToken stoppingToken)
    {
        using var cleanupActivity = BotActivitySource.StartBackgroundCleanupActivity(
            TracingServiceName,
            "activity_events");

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.Value.ActivityEventRetentionDays);
            var batchSize = _options.Value.CleanupBatchSize;
            var totalDeleted = 0;

            _logger.LogDebug("Cleaning up activity events older than {Cutoff}", cutoff);

            while (!stoppingToken.IsCancellationRequested)
            {
                var deleted = await activityEventRepo.DeleteBatchOlderThanAsync(cutoff, batchSize, stoppingToken);

                if (deleted == 0)
                {
                    break; // No more records to delete
                }

                totalDeleted += deleted;
                _logger.LogDebug("Deleted {Count} activity events (batch)", deleted);

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
                _logger.LogInformation("Deleted {Count} activity events older than {RetentionDays} days",
                    totalDeleted, _options.Value.ActivityEventRetentionDays);
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
