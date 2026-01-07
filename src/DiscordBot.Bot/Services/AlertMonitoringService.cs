using System.Collections.Concurrent;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that monitors performance metrics and creates/resolves incidents.
/// Evaluates alert configurations at regular intervals and triggers alerts when thresholds are breached.
/// Also implements <see cref="IMetricsProvider"/> to expose current metric values for display.
/// </summary>
public class AlertMonitoringService : BackgroundService, IBackgroundServiceHealth, IMetricsProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPerformanceNotifier _performanceNotifier;
    private readonly ILogger<AlertMonitoringService> _logger;
    private readonly PerformanceAlertOptions _options;

    // Track breach counts per metric in memory
    private readonly ConcurrentDictionary<string, int> _breachCounts = new();
    private readonly ConcurrentDictionary<string, int> _normalCounts = new();

    // Health tracking
    private DateTime? _lastHeartbeat;
    private string? _lastError;
    private string _status = "Initializing";

    // Lazily resolved services (to avoid circular dependency during DI resolution)
    private ILatencyHistoryService? _latencyHistoryService;
    private ICommandPerformanceAggregator? _commandPerformanceAggregator;
    private IApiRequestTracker? _apiRequestTracker;
    private IDatabaseMetricsCollector? _databaseMetricsCollector;
    private IConnectionStateService? _connectionStateService;
    private IBackgroundServiceHealthRegistry? _healthRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlertMonitoringService"/> class.
    /// Uses minimal dependencies to avoid circular DI resolution issues.
    /// </summary>
    public AlertMonitoringService(
        IServiceProvider serviceProvider,
        IPerformanceNotifier performanceNotifier,
        ILogger<AlertMonitoringService> logger,
        IOptions<PerformanceAlertOptions> options)
    {
        _serviceProvider = serviceProvider;
        _performanceNotifier = performanceNotifier;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Resolves required services lazily after startup is complete.
    /// </summary>
    private void ResolveServices()
    {
        _latencyHistoryService = _serviceProvider.GetRequiredService<ILatencyHistoryService>();
        _commandPerformanceAggregator = _serviceProvider.GetRequiredService<ICommandPerformanceAggregator>();
        _apiRequestTracker = _serviceProvider.GetRequiredService<IApiRequestTracker>();
        _databaseMetricsCollector = _serviceProvider.GetRequiredService<IDatabaseMetricsCollector>();
        _connectionStateService = _serviceProvider.GetRequiredService<IConnectionStateService>();
        _healthRegistry = _serviceProvider.GetRequiredService<IBackgroundServiceHealthRegistry>();
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

        // Resolve services lazily after startup is complete
        ResolveServices();

        // Register with health registry
        _healthRegistry!.Register(ServiceName, this);

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
            var currentValue = await GetCurrentMetricValueAsync(config.MetricName);

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
    /// Handles a threshold breach by incrementing breach count and creating incident if needed.
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
            var existingIncident = await repository.GetActiveIncidentByMetricAsync(config.MetricName, cancellationToken);

            if (existingIncident == null)
            {
                // Create new incident
                var incident = new PerformanceIncident
                {
                    Id = Guid.NewGuid(),
                    MetricName = config.MetricName,
                    Severity = severity,
                    Status = IncidentStatus.Active,
                    TriggeredAt = DateTime.UtcNow,
                    ThresholdValue = threshold,
                    ActualValue = currentValue,
                    Message = $"{config.DisplayName} exceeded {severity.ToString().ToLower()} threshold: {currentValue:F2}{config.ThresholdUnit} >= {threshold:F2}{config.ThresholdUnit}"
                };

                var createdIncident = await repository.CreateIncidentAsync(incident, cancellationToken);

                _logger.LogWarning(
                    "Alert triggered for {MetricName}: {Message}",
                    config.MetricName,
                    incident.Message);

                // Broadcast via SignalR using the notifier
                var dto = MapToIncidentDto(createdIncident);
                await _performanceNotifier.BroadcastAlertTriggeredAsync(dto, cancellationToken);

                // Reset breach count after creating incident
                _breachCounts.TryRemove(config.MetricName, out _);
            }
            else
            {
                _logger.LogTrace(
                    "Active incident already exists for {MetricName}, not creating duplicate",
                    config.MetricName);
            }
        }
    }

    /// <summary>
    /// Handles a normal reading by incrementing normal count and auto-resolving incident if needed.
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
            var existingIncident = await repository.GetActiveIncidentByMetricAsync(config.MetricName, cancellationToken);

            if (existingIncident != null)
            {
                existingIncident.Status = IncidentStatus.Resolved;
                existingIncident.ResolvedAt = DateTime.UtcNow;

                var resolvedIncident = await repository.UpdateIncidentAsync(existingIncident, cancellationToken);

                _logger.LogInformation(
                    "Auto-resolved incident for {MetricName} after {NormalCount} consecutive normal readings",
                    config.MetricName,
                    normalCount);

                // Broadcast via SignalR using the notifier
                var dto = MapToIncidentDto(resolvedIncident);
                await _performanceNotifier.BroadcastAlertResolvedAsync(dto, cancellationToken);

                // Reset normal count after resolving
                _normalCounts.TryRemove(config.MetricName, out _);
            }
        }
    }


    /// <summary>
    /// Gets the current value for a specific metric.
    /// Returns null if the metric is not available or cannot be measured.
    /// </summary>
    private async Task<double?> GetCurrentMetricValueAsync(string metricName)
    {
        try
        {
            // Metric names must match the seeded data in the migration (snake_case)
            return metricName switch
            {
                "gateway_latency" => GetGatewayLatency(),
                "command_p95_latency" => await GetCommandP95LatencyAsync(),
                "error_rate" => await GetErrorRateAsync(),
                "memory_usage" => GetMemoryUsage(),
                "api_rate_limit_usage" => GetApiRateLimitUsage(),
                "database_query_time" => GetDatabaseQueryTime(),
                "bot_disconnected" => IsBotDisconnected() ? 1.0 : 0.0,
                "service_failure" => HasServiceFailure() ? 1.0 : 0.0,
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting current value for metric {MetricName}", metricName);
            return null;
        }
    }

    /// <summary>
    /// Gets the current gateway latency in milliseconds.
    /// </summary>
    private double? GetGatewayLatency()
    {
        var latency = _latencyHistoryService!.GetCurrentLatency();
        return latency > 0 ? latency : null;
    }

    /// <summary>
    /// Gets the command P95 latency in milliseconds.
    /// Calculates a weighted P95 across all commands.
    /// </summary>
    private async Task<double?> GetCommandP95LatencyAsync()
    {
        var aggregates = await _commandPerformanceAggregator!.GetAggregatesAsync(1); // Last hour

        if (!aggregates.Any())
        {
            return null;
        }

        // Calculate weighted P95 (weight by execution count)
        var totalExecutions = aggregates.Sum(a => a.ExecutionCount);
        if (totalExecutions == 0)
        {
            return null;
        }

        var weightedP95 = aggregates.Sum(a => a.P95Ms * a.ExecutionCount) / totalExecutions;
        return weightedP95;
    }

    /// <summary>
    /// Gets the overall error rate as a percentage.
    /// </summary>
    private async Task<double?> GetErrorRateAsync()
    {
        var aggregates = await _commandPerformanceAggregator!.GetAggregatesAsync(1); // Last hour

        if (!aggregates.Any())
        {
            return null;
        }

        var totalExecutions = aggregates.Sum(a => a.ExecutionCount);
        if (totalExecutions == 0)
        {
            return null;
        }

        // Calculate overall error rate
        var weightedErrorRate = aggregates.Sum(a => a.ErrorRate * a.ExecutionCount) / totalExecutions;
        return weightedErrorRate;
    }

    /// <summary>
    /// Gets the current memory usage in megabytes.
    /// </summary>
    private double? GetMemoryUsage()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var memoryMb = process.WorkingSet64 / (1024.0 * 1024.0);
        return memoryMb;
    }

    /// <summary>
    /// Gets the API rate limit usage as a count of rate limit hits.
    /// </summary>
    private double? GetApiRateLimitUsage()
    {
        var rateLimitEvents = _apiRequestTracker!.GetRateLimitEvents(1); // Last hour
        return rateLimitEvents.Count;
    }

    /// <summary>
    /// Gets the database query time in milliseconds (average).
    /// </summary>
    private double? GetDatabaseQueryTime()
    {
        var metrics = _databaseMetricsCollector!.GetMetrics();
        return metrics.AvgQueryTimeMs > 0 ? metrics.AvgQueryTimeMs : null;
    }

    /// <summary>
    /// Checks if the bot is currently disconnected.
    /// </summary>
    private bool IsBotDisconnected()
    {
        var state = _connectionStateService!.GetCurrentState();
        return state != GatewayConnectionState.Connected;
    }

    /// <summary>
    /// Checks if any background service has failed.
    /// </summary>
    private bool HasServiceFailure()
    {
        var services = _healthRegistry!.GetAllHealth();
        return services.Any(s => s.Status == "Error" || s.Status == "Unhealthy");
    }

    /// <summary>
    /// Maps a PerformanceIncident entity to a PerformanceIncidentDto.
    /// </summary>
    private static PerformanceIncidentDto MapToIncidentDto(PerformanceIncident incident)
    {
        double? durationSeconds = null;

        if (incident.ResolvedAt.HasValue)
        {
            durationSeconds = (incident.ResolvedAt.Value - incident.TriggeredAt).TotalSeconds;
        }

        return new PerformanceIncidentDto
        {
            Id = incident.Id,
            MetricName = incident.MetricName,
            Severity = incident.Severity,
            Status = incident.Status,
            TriggeredAt = incident.TriggeredAt,
            ResolvedAt = incident.ResolvedAt,
            ThresholdValue = incident.ThresholdValue,
            ActualValue = incident.ActualValue,
            Message = incident.Message,
            IsAcknowledged = incident.IsAcknowledged,
            AcknowledgedBy = incident.AcknowledgedBy,
            AcknowledgedAt = incident.AcknowledgedAt,
            Notes = incident.Notes,
            DurationSeconds = durationSeconds
        };
    }

    #region IMetricsProvider Implementation

    /// <inheritdoc/>
    public async Task<double?> GetCurrentValueAsync(string metricName)
    {
        // Ensure services are resolved (they might not be if called early)
        if (_latencyHistoryService == null)
        {
            ResolveServices();
        }

        return await GetCurrentMetricValueAsync(metricName);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, double?>> GetAllCurrentValuesAsync()
    {
        // Ensure services are resolved (they might not be if called early)
        if (_latencyHistoryService == null)
        {
            ResolveServices();
        }

        var metricNames = new[]
        {
            "gateway_latency",
            "command_p95_latency",
            "error_rate",
            "memory_usage",
            "api_rate_limit_usage",
            "database_query_time",
            "bot_disconnected",
            "service_failure"
        };

        var results = new Dictionary<string, double?>();

        foreach (var metricName in metricNames)
        {
            results[metricName] = await GetCurrentMetricValueAsync(metricName);
        }

        return results;
    }

    #endregion
}
