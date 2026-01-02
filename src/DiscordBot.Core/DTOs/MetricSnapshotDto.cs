namespace DiscordBot.Core.DTOs;

/// <summary>
/// DTO representing a metric snapshot or aggregated bucket.
/// </summary>
public record MetricSnapshotDto
{
    /// <summary>
    /// Timestamp of the snapshot or bucket start time (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Average database query time in milliseconds.
    /// </summary>
    public double DatabaseAvgQueryTimeMs { get; init; }

    /// <summary>
    /// Working set memory in MB.
    /// </summary>
    public long WorkingSetMB { get; init; }

    /// <summary>
    /// Heap size in MB.
    /// </summary>
    public long HeapSizeMB { get; init; }

    /// <summary>
    /// Cache hit rate percentage.
    /// </summary>
    public double CacheHitRatePercent { get; init; }

    /// <summary>
    /// Number of running background services.
    /// </summary>
    public int ServicesRunningCount { get; init; }

    /// <summary>
    /// Gen 0 GC collections (delta for aggregated buckets).
    /// </summary>
    public int Gen0Collections { get; init; }

    /// <summary>
    /// Gen 1 GC collections (delta for aggregated buckets).
    /// </summary>
    public int Gen1Collections { get; init; }

    /// <summary>
    /// Gen 2 GC collections (delta for aggregated buckets).
    /// </summary>
    public int Gen2Collections { get; init; }
}
