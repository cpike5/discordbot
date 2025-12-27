using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically cleans up old audit logs according to the retention policy.
/// Respects the configured retention period and runs cleanup at scheduled intervals.
/// </summary>
public class AuditLogRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AuditLogRetentionOptions> _options;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;
    private readonly ILogger<AuditLogRetentionService> _logger;

    public AuditLogRetentionService(
        IServiceScopeFactory scopeFactory,
        IOptions<AuditLogRetentionOptions> options,
        IOptions<BackgroundServicesOptions> bgOptions,
        ILogger<AuditLogRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _bgOptions = bgOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
                _logger.LogError(ex, "Error during audit log cleanup");
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
    private async Task PerformCleanupAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting audit log cleanup. Retention: {RetentionDays} days, Batch size: {BatchSize}",
            _options.Value.RetentionDays,
            _options.Value.CleanupBatchSize);

        using var scope = _scopeFactory.CreateScope();
        var auditLogRepository = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

        var cutoffDate = DateTime.UtcNow.AddDays(-_options.Value.RetentionDays);
        var deletedCount = await auditLogRepository.DeleteOlderThanAsync(cutoffDate, stoppingToken);

        _logger.LogInformation(
            "Audit log cleanup completed. Deleted {DeletedCount} logs older than {RetentionDays} days (cutoff date: {CutoffDate:yyyy-MM-dd})",
            deletedCount,
            _options.Value.RetentionDays,
            cutoffDate);
    }
}
