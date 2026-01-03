using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically cleans up old audit logs according to the retention policy.
/// Respects the configured retention period and runs cleanup at scheduled intervals.
/// </summary>
public class AuditLogRetentionService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AuditLogRetentionOptions> _options;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;

    public override string ServiceName => "Audit Log Retention Service";

    /// <summary>
    /// Gets the service name formatted for tracing (snake_case).
    /// </summary>
    private string TracingServiceName => "audit_log_retention_service";

    public AuditLogRetentionService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<AuditLogRetentionOptions> options,
        IOptions<BackgroundServicesOptions> bgOptions,
        ILogger<AuditLogRetentionService> logger)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _bgOptions = bgOptions;
    }

    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit log retention service starting");

        // Check if cleanup is enabled
        if (!_options.Value.Enabled)
        {
            _logger.LogInformation("Audit log retention cleanup is disabled via configuration");
            return;
        }

        _logger.LogInformation(
            "Audit log retention service enabled. Retention: {RetentionDays} days, Interval: {IntervalHours} hours, Batch size: {BatchSize}",
            _options.Value.RetentionDays,
            _options.Value.CleanupIntervalHours,
            _options.Value.CleanupBatchSize);

        // Initial delay to let the app start up
        var initialDelay = TimeSpan.FromMinutes(_bgOptions.Value.AuditLogCleanupInitialDelayMinutes);
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
                var deletedCount = await PerformCleanupAsync(stoppingToken);

                BotActivitySource.SetRecordsDeleted(activity, deletedCount);
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
                _logger.LogError(ex, "Error during audit log cleanup");
                RecordError(ex);
            }

            // Wait for next cleanup interval
            var interval = TimeSpan.FromHours(_options.Value.CleanupIntervalHours);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Audit log retention service stopping");
    }

    /// <summary>
    /// Performs a single cleanup operation by creating a scoped service and invoking the cleanup method.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during cleanup.</param>
    /// <returns>The number of audit log records deleted.</returns>
    private async Task<int> PerformCleanupAsync(CancellationToken stoppingToken)
    {
        using var cleanupActivity = BotActivitySource.StartBackgroundCleanupActivity(
            TracingServiceName,
            "audit_logs");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var retentionDays = await GetRetentionDaysAsync(scope);

            _logger.LogInformation(
                "Starting audit log cleanup. Retention: {RetentionDays} days, Batch size: {BatchSize}",
                retentionDays,
                _options.Value.CleanupBatchSize);

            var auditLogRepository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

            var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
            var deletedCount = await auditLogRepository.DeleteOlderThanAsync(cutoffDate, stoppingToken);

            _logger.LogInformation(
                "Audit log cleanup completed. Deleted {DeletedCount} logs older than {RetentionDays} days (cutoff date: {CutoffDate:yyyy-MM-dd})",
                deletedCount,
                retentionDays,
                cutoffDate);

            BotActivitySource.SetRecordsDeleted(cleanupActivity, deletedCount);
            BotActivitySource.SetSuccess(cleanupActivity);

            return deletedCount;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(cleanupActivity, ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the retention days from settings service with fallback to IOptions.
    /// </summary>
    /// <param name="scope">The service scope to resolve settings service from.</param>
    /// <returns>The number of days to retain audit logs.</returns>
    private async Task<int> GetRetentionDaysAsync(IServiceScope scope)
    {
        try
        {
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var settingValue = await settingsService.GetSettingValueAsync<int?>("Advanced:AuditLogRetentionDays");

            if (settingValue.HasValue && settingValue.Value > 0)
            {
                _logger.LogDebug("Using audit log retention from settings: {RetentionDays} days", settingValue.Value);
                return settingValue.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get audit log retention from settings, falling back to configuration");
        }

        // Fall back to IOptions
        return _options.Value.RetentionDays;
    }
}
