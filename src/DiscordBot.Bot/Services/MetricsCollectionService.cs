using System.Diagnostics;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that periodically collects system health metrics and persists them to the database.
/// Samples metrics at a configurable interval and implements automatic cleanup of old snapshots.
/// Extends <see cref="MonitoredBackgroundService"/> for health monitoring integration.
/// </summary>
public class MetricsCollectionService : MonitoredBackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HistoricalMetricsOptions _options;

    // Lazily resolved singleton services (to avoid circular DI issues)
    private IDatabaseMetricsCollector? _databaseMetricsCollector;
    private IInstrumentedCache? _instrumentedCache;
    private IBackgroundServiceHealthRegistry? _healthRegistry;
    private ICpuHistoryService? _cpuHistoryService;

    // Track last cleanup time to schedule periodic cleanup
    private DateTime? _lastCleanup;

    /// <inheritdoc/>
    public override string ServiceName => "MetricsCollectionService";

    protected virtual string TracingServiceName => "metrics_collection_service";

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsCollectionService"/> class.
    /// Uses minimal dependencies to avoid circular DI resolution issues.
    /// </summary>
    public MetricsCollectionService(
        IServiceProvider serviceProvider,
        ILogger<MetricsCollectionService> logger,
        IOptions<HistoricalMetricsOptions> options)
        : base(serviceProvider, logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    /// <summary>
    /// Resolves required singleton services lazily after startup is complete.
    /// </summary>
    private void ResolveServices()
    {
        _databaseMetricsCollector = _serviceProvider.GetRequiredService<IDatabaseMetricsCollector>();
        _instrumentedCache = _serviceProvider.GetRequiredService<IInstrumentedCache>();
        _healthRegistry = _serviceProvider.GetRequiredService<IBackgroundServiceHealthRegistry>();
        _cpuHistoryService = _serviceProvider.GetRequiredService<ICpuHistoryService>();
    }

    /// <inheritdoc/>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        // Yield immediately to prevent blocking startup
        await Task.Yield();

        // Check if metrics collection is enabled
        if (!_options.Enabled)
        {
            _logger.LogInformation("Historical metrics collection is disabled in configuration");
            SetStatus("Disabled");
            return;
        }

        _logger.LogInformation("MetricsCollectionService starting with sample interval: {Interval}s, retention: {Retention} days",
            _options.SampleIntervalSeconds,
            _options.RetentionDays);

        // Resolve services lazily after startup is complete
        ResolveServices();

        // Small initial delay to let other services initialize
        await Task.Delay(TimeSpan.FromSeconds(_options.InitialDelaySeconds), stoppingToken);

        _logger.LogInformation("MetricsCollectionService initialized, starting collection loop");

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
                // Collect and persist snapshot
                await CollectAndPersistSnapshotAsync(stoppingToken);

                // Check if cleanup is needed
                await PerformCleanupIfNeededAsync(stoppingToken);

                // Update heartbeat on successful iteration
                UpdateHeartbeat();
                BotActivitySource.SetRecordsProcessed(activity, 1); // 1 snapshot collected
                BotActivitySource.SetSuccess(activity);
                ClearError();

                // Wait for the configured interval
                await Task.Delay(TimeSpan.FromSeconds(_options.SampleIntervalSeconds), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                RecordError(ex);
                _logger.LogError(ex, "Error in metrics collection loop");
                BotActivitySource.RecordException(activity, ex);

                // Wait a bit before retrying after an error
                await Task.Delay(TimeSpan.FromSeconds(_options.ErrorRetryDelaySeconds), stoppingToken);
            }
        }
    }

    /// <summary>
    /// Collects metrics from all sources and persists a snapshot to the database.
    /// </summary>
    private async Task CollectAndPersistSnapshotAsync(CancellationToken cancellationToken)
    {
        var snapshot = new MetricSnapshot
        {
            Timestamp = DateTime.UtcNow
        };

        // Collect database metrics
        var dbMetrics = _databaseMetricsCollector!.GetMetrics();
        snapshot.DatabaseAvgQueryTimeMs = dbMetrics.AvgQueryTimeMs;
        snapshot.DatabaseTotalQueries = dbMetrics.TotalQueries;
        snapshot.DatabaseSlowQueryCount = dbMetrics.SlowQueryCount;

        // Collect memory metrics
        using var process = Process.GetCurrentProcess();
        snapshot.WorkingSetMB = process.WorkingSet64 / (1024 * 1024);
        snapshot.PrivateMemoryMB = process.PrivateMemorySize64 / (1024 * 1024);
        snapshot.HeapSizeMB = GC.GetTotalMemory(false) / (1024 * 1024);

        // Collect GC metrics
        snapshot.Gen0Collections = GC.CollectionCount(0);
        snapshot.Gen1Collections = GC.CollectionCount(1);
        snapshot.Gen2Collections = GC.CollectionCount(2);

        // Collect cache metrics (aggregate all prefixes)
        var cacheStats = _instrumentedCache!.GetStatistics();
        if (cacheStats.Any())
        {
            var totalHits = cacheStats.Sum(s => s.Hits);
            var totalMisses = cacheStats.Sum(s => s.Misses);
            var totalRequests = totalHits + totalMisses;

            snapshot.CacheHitRatePercent = totalRequests > 0
                ? (totalHits / (double)totalRequests) * 100.0
                : 0.0;
            snapshot.CacheTotalEntries = cacheStats.Sum(s => s.Size);
            snapshot.CacheTotalHits = totalHits;
            snapshot.CacheTotalMisses = totalMisses;
        }
        else
        {
            snapshot.CacheHitRatePercent = 0.0;
            snapshot.CacheTotalEntries = 0;
            snapshot.CacheTotalHits = 0;
            snapshot.CacheTotalMisses = 0;
        }

        // Collect service health metrics
        var services = _healthRegistry!.GetAllHealth();
        snapshot.ServicesTotalCount = services.Count;
        snapshot.ServicesRunningCount = services.Count(s => s.Status == "Running");
        snapshot.ServicesErrorCount = services.Count(s => s.Status == "Error" || s.Status == "Unhealthy");

        // Collect CPU metrics
        snapshot.CpuUsagePercent = _cpuHistoryService!.GetCurrentCpu();

        // Persist snapshot to database using scoped repository
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMetricSnapshotRepository>();

        await repository.AddAsync(snapshot, cancellationToken);

        _logger.LogTrace("Collected metric snapshot: DB {DbAvg}ms, Memory {Memory}MB, Cache {CacheHit:F1}%, Services {Running}/{Total}, CPU {CpuPercent:F1}%",
            snapshot.DatabaseAvgQueryTimeMs,
            snapshot.WorkingSetMB,
            snapshot.CacheHitRatePercent,
            snapshot.ServicesRunningCount,
            snapshot.ServicesTotalCount,
            snapshot.CpuUsagePercent);
    }

    /// <summary>
    /// Performs cleanup of old snapshots if the cleanup interval has elapsed.
    /// </summary>
    private async Task PerformCleanupIfNeededAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Check if cleanup is needed
        if (_lastCleanup.HasValue)
        {
            var timeSinceLastCleanup = now - _lastCleanup.Value;
            if (timeSinceLastCleanup < TimeSpan.FromHours(_options.CleanupIntervalHours))
            {
                return; // Not time yet
            }
        }

        // Perform cleanup
        var cutoffDate = now.AddDays(-_options.RetentionDays);

        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMetricSnapshotRepository>();

        var deletedCount = await repository.DeleteOlderThanAsync(cutoffDate, cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Deleted {Count} metric snapshots older than {CutoffDate:yyyy-MM-dd}",
                deletedCount,
                cutoffDate);
        }
        else
        {
            _logger.LogTrace("Cleanup completed, no snapshots older than {CutoffDate:yyyy-MM-dd} found",
                cutoffDate);
        }

        _lastCleanup = now;
    }
}
