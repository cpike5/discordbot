using System.Collections.Concurrent;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that orchestrates performance metric monitoring.
/// Evaluates alert configurations at regular intervals and delegates metric collection
/// to <see cref="IMetricValueCollector"/> and incident lifecycle management to
/// <see cref="IAlertIncidentManager"/>.
/// Also implements <see cref="IMetricsProvider"/> to expose current metric values for display.
/// </summary>
public class AlertMonitoringService : BackgroundService, IBackgroundServiceHealth, IMetricsProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMetricValueCollector _metricValueCollector;
    private readonly IAlertIncidentManager _alertIncidentManager;
    private readonly ILogger<AlertMonitoringService> _logger;
    private readonly PerformanceAlertOptions _options;

    // Track breach counts per metric in memory
    private readonly ConcurrentDictionary<string, int> _breachCounts = new();
    private readonly ConcurrentDictionary<string, int> _normalCounts = new();

    // Health tracking
    private DateTime? _lastHeartbeat;
    private string? _lastError;
    private string _status = "Initializing";

    // Lazily resolved health registry (to avoid circular dependency during DI resolution)
    private IBackgroundServiceHealthRegistry? _healthRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlertMonitoringService"/> class.
    /// </summary>
    public AlertMonitoringService(
        IServiceProvider serviceProvider,
        IMetricValueCollector metricValueCollector,
        IAlertIncidentManager alertIncidentManager,
        ILogger<AlertMonitoringService> logger,
        IOptions<PerformanceAlertOptions> options)
    {
        _serviceProvider = serviceProvider;
        _metricValueCollector = metricValueCollector;
        _alertIncidentManager = alertIncidentManager;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public string ServiceName => "AlertMonitoringService";

    protected virtual string TracingServiceName => "alert_monitoring_service";

    /// <inheritdoc/>
    public string Status => _status;

    /// <inheritdoc/>
    public DateTime? LastHeartbeat => _lastHeartbeat;

    /// <inheritdoc/>
    public string? LastError => _lastError;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield immediately to prevent blocking host startup
        await Task.Yield();

        _logger.LogInformation("AlertMonitoringService starting");

        // Resolve health registry lazily after startup is complete
        _healthRegistry = _serviceProvider.GetRequiredService<IBackgroundServiceHealthRegistry>();
        _healthRegistry.Register(ServiceName, this);

        try
        {
            // Delay initial start to let other services initialize
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

            _status = "Running";
            _logger.LogInformation("AlertMonitoringService initialized, starting monitoring loop");

            var executionCycle = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                executionCycle++;
                var correlationId = Guid.NewGuid().ToString("N")[..16];

                using var activity = BotActivitySource.StartBackgroundServiceActivity(
                    TracingServiceName,
                    executionCycle,
                    correlationId);

                try
                {
                    // Record heartbeat
                    _lastHeartbeat = DateTime.UtcNow;

                    var configsChecked = await MonitorMetricsAsync(stoppingToken);
                    BotActivitySource.SetRecordsProcessed(activity, configsChecked);
                    BotActivitySource.SetSuccess(activity);

                    // Wait for the configured interval
                    await Task.Delay(TimeSpan.FromSeconds(_options.CheckIntervalSeconds), stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _lastError = ex.Message;
                    _status = "Error";
                    _logger.LogError(ex, "Error in alert monitoring loop");
                    BotActivitySource.RecordException(activity, ex);

                    // Wait a bit before retrying after an error
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    _status = "Running";
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("AlertMonitoringService stopping due to cancellation");
        }
        finally
        {
            _status = "Stopped";
            _healthRegistry?.Unregister(ServiceName);
            _logger.LogInformation("AlertMonitoringService stopped");
        }
    }

    /// <summary>
    /// Monitors all enabled alert configurations and checks for threshold breaches.
    /// </summary>
    /// <returns>The number of alert configurations checked.</returns>
    private async Task<int> MonitorMetricsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPerformanceAlertRepository>();

        var configs = await repository.GetAllConfigsAsync(cancellationToken);
        var enabledConfigs = configs.Where(c => c.IsEnabled).ToList();

        _logger.LogTrace("Checking {Count} enabled alert configurations", enabledConfigs.Count);

        foreach (var config in enabledConfigs)
        {
            await CheckMetricAsync(config, repository, cancellationToken);
        }

        return enabledConfigs.Count;
    }

    /// <summary>
    /// Checks a single metric against its configured thresholds.
    /// </summary>
    private async Task CheckMetricAsync(
        PerformanceAlertConfig config,
        IPerformanceAlertRepository repository,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentValue = await _metricValueCollector.GetCurrentMetricValueAsync(config.MetricName);

            if (currentValue == null)
            {
                _logger.LogTrace("No value available for metric {MetricName}", config.MetricName);
                return;
            }

            // Determine if thresholds are breached
            var (isBreached, severity, threshold) = CheckThresholds(config, currentValue.Value);

            if (isBreached)
            {
                await HandleThresholdBreachAsync(config, currentValue.Value, severity, threshold, repository, cancellationToken);
            }
            else
            {
                await HandleNormalReadingAsync(config, repository, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking metric {MetricName}", config.MetricName);
        }
    }

    /// <summary>
    /// Checks if a metric value exceeds configured thresholds.
    /// Returns (isBreached, severity, threshold).
    /// </summary>
    private static (bool IsBreached, AlertSeverity Severity, double Threshold) CheckThresholds(
        PerformanceAlertConfig config,
        double currentValue)
    {
        // Check critical threshold first (higher priority)
        if (config.CriticalThreshold.HasValue && currentValue >= config.CriticalThreshold.Value)
        {
            return (true, AlertSeverity.Critical, config.CriticalThreshold.Value);
        }

        // Check warning threshold
        if (config.WarningThreshold.HasValue && currentValue >= config.WarningThreshold.Value)
        {
            return (true, AlertSeverity.Warning, config.WarningThreshold.Value);
        }

        return (false, AlertSeverity.Info, 0);
    }

    /// <summary>
    /// Handles a threshold breach by incrementing breach count and delegating incident creation if needed.
    /// </summary>
    private async Task HandleThresholdBreachAsync(
        PerformanceAlertConfig config,
        double currentValue,
        AlertSeverity severity,
        double threshold,
        IPerformanceAlertRepository repository,
        CancellationToken cancellationToken)
    {
        var breachCount = _breachCounts.AddOrUpdate(config.MetricName, 1, (_, count) => count + 1);
        _normalCounts.TryRemove(config.MetricName, out _); // Reset normal count

        _logger.LogDebug(
            "Threshold breach for {MetricName}: {CurrentValue} >= {Threshold} ({Severity}), breach count: {BreachCount}",
            config.MetricName,
            currentValue,
            threshold,
            severity,
            breachCount);

        // Create incident if consecutive breaches requirement is met
        if (breachCount >= _options.ConsecutiveBreachesRequired)
        {
            var created = await _alertIncidentManager.HandleBreachAsync(
                config, currentValue, severity, threshold, repository, cancellationToken);

            if (created)
            {
                // Reset breach count after creating incident
                _breachCounts.TryRemove(config.MetricName, out _);
            }
        }
    }

    /// <summary>
    /// Handles a normal reading by incrementing normal count and delegating incident resolution if needed.
    /// </summary>
    private async Task HandleNormalReadingAsync(
        PerformanceAlertConfig config,
        IPerformanceAlertRepository repository,
        CancellationToken cancellationToken)
    {
        var normalCount = _normalCounts.AddOrUpdate(config.MetricName, 1, (_, count) => count + 1);
        _breachCounts.TryRemove(config.MetricName, out _); // Reset breach count

        _logger.LogTrace(
            "Normal reading for {MetricName}, normal count: {NormalCount}",
            config.MetricName,
            normalCount);

        // Auto-resolve incident if consecutive normal readings requirement is met
        if (normalCount >= _options.ConsecutiveNormalRequired)
        {
            var resolved = await _alertIncidentManager.HandleResolutionAsync(
                config, repository, cancellationToken);

            if (resolved)
            {
                _logger.LogInformation(
                    "Auto-resolved incident for {MetricName} after {NormalCount} consecutive normal readings",
                    config.MetricName,
                    normalCount);

                // Reset normal count after resolving
                _normalCounts.TryRemove(config.MetricName, out _);
            }
        }
    }

    #region IMetricsProvider Implementation

    /// <inheritdoc/>
    public async Task<double?> GetCurrentValueAsync(string metricName)
    {
        return await _metricValueCollector.GetCurrentMetricValueAsync(metricName);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, double?>> GetAllCurrentValuesAsync()
    {
        return await _metricValueCollector.GetAllMetricValuesAsync();
    }

    #endregion
}
