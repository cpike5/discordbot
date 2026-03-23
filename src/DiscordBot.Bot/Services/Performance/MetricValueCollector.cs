using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Performance;

/// <summary>
/// Collects current metric values from various infrastructure services.
/// Provides a single point of access for reading live performance metrics
/// such as gateway latency, command P95, error rate, memory usage, etc.
/// </summary>
public class MetricValueCollector : IMetricValueCollector
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MetricValueCollector> _logger;

    // Lazily resolved services (to avoid circular dependency during DI resolution)
    private ILatencyHistoryService? _latencyHistoryService;
    private ICommandPerformanceAggregator? _commandPerformanceAggregator;
    private IApiRequestTracker? _apiRequestTracker;
    private IDatabaseMetricsCollector? _databaseMetricsCollector;
    private IConnectionStateService? _connectionStateService;
    private IBackgroundServiceHealthRegistry? _healthRegistry;
    private bool _servicesResolved;

    /// <summary>
    /// All known metric names supported by this collector.
    /// </summary>
    public static readonly IReadOnlyList<string> KnownMetricNames = new[]
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

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricValueCollector"/> class.
    /// </summary>
    public MetricValueCollector(
        IServiceProvider serviceProvider,
        ILogger<MetricValueCollector> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<double?> GetCurrentMetricValueAsync(string metricName)
    {
        EnsureServicesResolved();

        try
        {
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

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, double?>> GetAllMetricValuesAsync()
    {
        EnsureServicesResolved();

        var results = new Dictionary<string, double?>();

        foreach (var metricName in KnownMetricNames)
        {
            results[metricName] = await GetCurrentMetricValueAsync(metricName);
        }

        return results;
    }

    /// <summary>
    /// Ensures that dependent services are resolved from the DI container.
    /// Uses lazy resolution to avoid circular dependencies during startup.
    /// </summary>
    private void EnsureServicesResolved()
    {
        if (_servicesResolved) return;

        _latencyHistoryService = _serviceProvider.GetRequiredService<ILatencyHistoryService>();
        _commandPerformanceAggregator = _serviceProvider.GetRequiredService<ICommandPerformanceAggregator>();
        _apiRequestTracker = _serviceProvider.GetRequiredService<IApiRequestTracker>();
        _databaseMetricsCollector = _serviceProvider.GetRequiredService<IDatabaseMetricsCollector>();
        _connectionStateService = _serviceProvider.GetRequiredService<IConnectionStateService>();
        _healthRegistry = _serviceProvider.GetRequiredService<IBackgroundServiceHealthRegistry>();
        _servicesResolved = true;
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

        var weightedErrorRate = aggregates.Sum(a => a.ErrorRate * a.ExecutionCount) / totalExecutions;
        return weightedErrorRate;
    }

    /// <summary>
    /// Gets the current memory usage in megabytes.
    /// </summary>
    private static double? GetMemoryUsage()
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
}
