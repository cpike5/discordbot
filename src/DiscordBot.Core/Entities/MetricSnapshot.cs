namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a point-in-time snapshot of system health metrics.
/// Collected periodically by MetricsCollectionService.
/// </summary>
public class MetricSnapshot
{
    /// <summary>
    /// Primary key.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Timestamp when the snapshot was taken (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    // ===== Database Metrics =====

    /// <summary>
    /// Average database query time in milliseconds at snapshot time.
    /// </summary>
    public double DatabaseAvgQueryTimeMs { get; set; }

    /// <summary>
    /// Total database queries executed since application start.
    /// </summary>
    public long DatabaseTotalQueries { get; set; }

    /// <summary>
    /// Number of slow queries detected in the sample period.
    /// </summary>
    public int DatabaseSlowQueryCount { get; set; }

    // ===== Memory Metrics =====

    /// <summary>
    /// Process working set memory in megabytes.
    /// </summary>
    public long WorkingSetMB { get; set; }

    /// <summary>
    /// Private memory in megabytes.
    /// </summary>
    public long PrivateMemoryMB { get; set; }

    /// <summary>
    /// GC heap size in megabytes.
    /// </summary>
    public long HeapSizeMB { get; set; }

    // ===== GC Metrics =====

    /// <summary>
    /// Gen 0 garbage collection count since process start.
    /// </summary>
    public int Gen0Collections { get; set; }

    /// <summary>
    /// Gen 1 garbage collection count since process start.
    /// </summary>
    public int Gen1Collections { get; set; }

    /// <summary>
    /// Gen 2 garbage collection count since process start.
    /// </summary>
    public int Gen2Collections { get; set; }

    // ===== Cache Metrics =====

    /// <summary>
    /// Overall cache hit rate as percentage (0-100).
    /// </summary>
    public double CacheHitRatePercent { get; set; }

    /// <summary>
    /// Total cache entries at snapshot time.
    /// </summary>
    public int CacheTotalEntries { get; set; }

    /// <summary>
    /// Total cache hits since application start.
    /// </summary>
    public long CacheTotalHits { get; set; }

    /// <summary>
    /// Total cache misses since application start.
    /// </summary>
    public long CacheTotalMisses { get; set; }

    // ===== Service Health =====

    /// <summary>
    /// Number of background services in "Running" state.
    /// </summary>
    public int ServicesRunningCount { get; set; }

    /// <summary>
    /// Number of background services in error or stopped state.
    /// </summary>
    public int ServicesErrorCount { get; set; }

    /// <summary>
    /// Total registered background services.
    /// </summary>
    public int ServicesTotalCount { get; set; }
}
