using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically cleans up old notifications according to the retention policy.
/// Deletes dismissed, read, and optionally unread notifications older than configured retention periods.
/// </summary>
public class NotificationRetentionService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<NotificationRetentionOptions> _options;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;

    public override string ServiceName => "Notification Retention Service";

    /// <summary>
    /// Gets the service name formatted for tracing (snake_case).
    /// </summary>
    private string TracingServiceName => "notification_retention_service";

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationRetentionService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for health monitoring.</param>
    /// <param name="scopeFactory">The service scope factory for creating scoped services.</param>
    /// <param name="options">Notification retention configuration options.</param>
    /// <param name="bgOptions">Background services configuration options.</param>
    /// <param name="logger">The logger.</param>
    public NotificationRetentionService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationRetentionOptions> options,
        IOptions<BackgroundServicesOptions> bgOptions,
        ILogger<NotificationRetentionService> logger)
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
            _logger.LogInformation("Notification retention cleanup is disabled via configuration");
            return;
        }

        _logger.LogInformation("Notification retention service starting");

        _logger.LogInformation(
            "Notification retention enabled. Dismissed: {DismissedDays} days, Read: {ReadDays} days, Unread: {UnreadDays} days (0=never), Interval: {IntervalHours}h, Batch size: {BatchSize}",
            _options.Value.DismissedRetentionDays,
            _options.Value.ReadRetentionDays,
            _options.Value.UnreadRetentionDays,
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
                _logger.LogError(ex, "Error during notification retention cleanup");
                RecordError(ex);
            }

            // Wait for next cleanup interval
            var interval = TimeSpan.FromHours(_options.Value.CleanupIntervalHours);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Notification retention service stopping");
    }

    /// <summary>
    /// Performs a single cleanup operation by removing old notifications.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during cleanup.</param>
    /// <returns>The total number of notifications deleted across all categories.</returns>
    private async Task<int> PerformCleanupAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting notification retention cleanup");

        using var scope = _scopeFactory.CreateScope();
        var notificationRepo = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

        var totalDeleted = 0;

        // Phase 1: Clean up dismissed notifications
        totalDeleted += await CleanupDismissedNotificationsAsync(notificationRepo, stoppingToken);

        // Phase 2: Clean up read notifications
        totalDeleted += await CleanupReadNotificationsAsync(notificationRepo, stoppingToken);

        // Phase 3: Clean up unread notifications (if configured)
        if (_options.Value.UnreadRetentionDays > 0)
        {
            totalDeleted += await CleanupUnreadNotificationsAsync(notificationRepo, stoppingToken);
        }

        _logger.LogInformation(
            "Notification retention cleanup completed. Deleted {TotalCount} total notifications",
            totalDeleted);

        return totalDeleted;
    }

    /// <summary>
    /// Cleans up dismissed notifications older than the configured retention period.
    /// </summary>
    private async Task<int> CleanupDismissedNotificationsAsync(
        INotificationRepository notificationRepo,
        CancellationToken stoppingToken)
    {
        using var cleanupActivity = BotActivitySource.StartBackgroundCleanupActivity(
            TracingServiceName,
            "dismissed_notifications");

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.Value.DismissedRetentionDays);
            var batchSize = _options.Value.CleanupBatchSize;
            var totalDeleted = 0;

            _logger.LogDebug("Cleaning up dismissed notifications older than {Cutoff}", cutoff);

            while (!stoppingToken.IsCancellationRequested)
            {
                var deleted = await notificationRepo.DeleteDismissedOlderThanAsync(cutoff, batchSize, stoppingToken);

                if (deleted == 0)
                {
                    break; // No more records to delete
                }

                totalDeleted += deleted;
                _logger.LogDebug("Deleted {Count} dismissed notifications (batch)", deleted);

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
                _logger.LogInformation(
                    "Deleted {Count} dismissed notifications older than {RetentionDays} days",
                    totalDeleted, _options.Value.DismissedRetentionDays);
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
    /// Cleans up read notifications older than the configured retention period.
    /// </summary>
    private async Task<int> CleanupReadNotificationsAsync(
        INotificationRepository notificationRepo,
        CancellationToken stoppingToken)
    {
        using var cleanupActivity = BotActivitySource.StartBackgroundCleanupActivity(
            TracingServiceName,
            "read_notifications");

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.Value.ReadRetentionDays);
            var batchSize = _options.Value.CleanupBatchSize;
            var totalDeleted = 0;

            _logger.LogDebug("Cleaning up read notifications older than {Cutoff}", cutoff);

            while (!stoppingToken.IsCancellationRequested)
            {
                var deleted = await notificationRepo.DeleteReadOlderThanAsync(cutoff, batchSize, stoppingToken);

                if (deleted == 0)
                {
                    break; // No more records to delete
                }

                totalDeleted += deleted;
                _logger.LogDebug("Deleted {Count} read notifications (batch)", deleted);

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
                _logger.LogInformation(
                    "Deleted {Count} read notifications older than {RetentionDays} days",
                    totalDeleted, _options.Value.ReadRetentionDays);
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
    /// Cleans up unread notifications older than the configured retention period.
    /// Only runs if UnreadRetentionDays > 0.
    /// </summary>
    private async Task<int> CleanupUnreadNotificationsAsync(
        INotificationRepository notificationRepo,
        CancellationToken stoppingToken)
    {
        using var cleanupActivity = BotActivitySource.StartBackgroundCleanupActivity(
            TracingServiceName,
            "unread_notifications");

        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.Value.UnreadRetentionDays);
            var batchSize = _options.Value.CleanupBatchSize;
            var totalDeleted = 0;

            _logger.LogDebug("Cleaning up unread notifications older than {Cutoff}", cutoff);

            while (!stoppingToken.IsCancellationRequested)
            {
                var deleted = await notificationRepo.DeleteUnreadOlderThanAsync(cutoff, batchSize, stoppingToken);

                if (deleted == 0)
                {
                    break; // No more records to delete
                }

                totalDeleted += deleted;
                _logger.LogDebug("Deleted {Count} unread notifications (batch)", deleted);

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
                _logger.LogInformation(
                    "Deleted {Count} unread notifications older than {RetentionDays} days",
                    totalDeleted, _options.Value.UnreadRetentionDays);
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
