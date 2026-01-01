namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for performance metrics collection and monitoring.
/// </summary>
public class PerformanceMetricsOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "PerformanceMetrics";

    /// <summary>
    /// Gets or sets the interval (in seconds) at which latency samples are recorded.
    /// Default is 30 seconds.
    /// </summary>
    public int LatencySampleIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the duration (in hours) for which latency history is retained.
    /// Default is 24 hours.
    /// </summary>
    public int LatencyRetentionHours { get; set; } = 24;

    /// <summary>
    /// Gets or sets the retention period (in days) for connection event history.
    /// Default is 7 days.
    /// </summary>
    public int ConnectionEventRetentionDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets whether Discord API request tracking is enabled.
    /// Default is true.
    /// </summary>
    public bool ApiRequestTrackingEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the threshold (in milliseconds) that defines a slow database query.
    /// Queries exceeding this duration are tracked separately.
    /// Default is 100ms.
    /// </summary>
    public int SlowQueryThresholdMs { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum number of slow queries to store in memory.
    /// Oldest entries are removed when this limit is exceeded.
    /// Default is 100.
    /// </summary>
    public int SlowQueryMaxStored { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether cache statistics tracking is enabled.
    /// Default is true.
    /// </summary>
    public bool CacheStatisticsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the time-to-live (in minutes) for cached command performance aggregations.
    /// Default is 5 minutes.
    /// </summary>
    public int CommandAggregationCacheTtlMinutes { get; set; } = 5;
}
