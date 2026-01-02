using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically cleans up old message logs according to the retention policy.
/// Respects the configured retention period and runs cleanup at scheduled intervals.
/// </summary>
public class MessageLogCleanupService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<MessageLogRetentionOptions> _options;
    private readonly IOptions<BackgroundServicesOptions> _bgOptions;

    public override string ServiceName => "Message Log Cleanup Service";

    public MessageLogCleanupService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<MessageLogRetentionOptions> options,
        IOptions<BackgroundServicesOptions> bgOptions,
        ILogger<MessageLogCleanupService> logger)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _bgOptions = bgOptions;
    }

    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
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

        // Initial delay to let the app start up
        var initialDelay = TimeSpan.FromMinutes(_bgOptions.Value.MessageLogCleanupInitialDelayMinutes);
        await Task.Delay(initialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            UpdateHeartbeat();

            try
            {
                await PerformCleanupAsync(stoppingToken);
                ClearError();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during message log cleanup");
                RecordError(ex);
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
        using var scope = _scopeFactory.CreateScope();
        var retentionDays = await GetRetentionDaysAsync(scope);

        _logger.LogInformation(
            "Starting message log cleanup. Retention: {RetentionDays} days, Batch size: {BatchSize}",
            retentionDays,
            _options.Value.CleanupBatchSize);

        var messageLogService = scope.ServiceProvider.GetRequiredService<IMessageLogService>();

        var deletedCount = await messageLogService.CleanupOldMessagesAsync(stoppingToken);

        _logger.LogInformation(
            "Message log cleanup completed. Deleted {DeletedCount} messages older than {RetentionDays} days",
            deletedCount,
            retentionDays);
    }

    /// <summary>
    /// Gets the retention days from settings service with fallback to IOptions.
    /// </summary>
    /// <param name="scope">The service scope to resolve settings service from.</param>
    /// <returns>The number of days to retain message logs.</returns>
    private async Task<int> GetRetentionDaysAsync(IServiceScope scope)
    {
        try
        {
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var settingValue = await settingsService.GetSettingValueAsync<int?>("Advanced:MessageLogRetentionDays");

            if (settingValue.HasValue && settingValue.Value > 0)
            {
                _logger.LogDebug("Using message log retention from settings: {RetentionDays} days", settingValue.Value);
                return settingValue.Value;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get message log retention from settings, falling back to configuration");
        }

        // Fall back to IOptions
        return _options.Value.RetentionDays;
    }
}
