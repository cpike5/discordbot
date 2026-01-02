namespace DiscordBot.Core.DTOs;

// ============================================================================
// Health & Status DTOs
// ============================================================================

/// <summary>
/// Overall performance health status with uptime and latency information.
/// </summary>
public record PerformanceHealthDto
{
    /// <summary>
    /// Gets or sets the overall health status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the bot uptime duration.
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Gets or sets the current gateway latency in milliseconds.
    /// </summary>
    public int LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the current connection state.
    /// </summary>
    public string ConnectionState { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this health snapshot was taken (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Latency history data with samples and statistical analysis.
/// </summary>
public record LatencyHistoryDto
{
    /// <summary>
    /// Gets or sets the collection of latency samples.
    /// </summary>
    public IReadOnlyList<LatencySampleDto> Samples { get; set; } = Array.Empty<LatencySampleDto>();

    /// <summary>
    /// Gets or sets the statistical summary of the latency data.
    /// </summary>
    public LatencyStatisticsDto Statistics { get; set; } = new();
}

/// <summary>
/// A single latency measurement sample.
/// </summary>
public record LatencySampleDto
{
    /// <summary>
    /// Gets or sets the timestamp when the sample was recorded (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the latency value in milliseconds.
    /// </summary>
    public int LatencyMs { get; set; }
}

/// <summary>
/// Statistical analysis of latency samples.
/// </summary>
public record LatencyStatisticsDto
{
    /// <summary>
    /// Gets or sets the average latency in milliseconds.
    /// </summary>
    public double Average { get; set; }

    /// <summary>
    /// Gets or sets the minimum latency in milliseconds.
    /// </summary>
    public int Min { get; set; }

    /// <summary>
    /// Gets or sets the maximum latency in milliseconds.
    /// </summary>
    public int Max { get; set; }

    /// <summary>
    /// Gets or sets the 50th percentile (median) latency in milliseconds.
    /// </summary>
    public int P50 { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile latency in milliseconds.
    /// </summary>
    public int P95 { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile latency in milliseconds.
    /// </summary>
    public int P99 { get; set; }

    /// <summary>
    /// Gets or sets the total number of samples included in this statistical analysis.
    /// </summary>
    public int SampleCount { get; set; }
}

// ============================================================================
// Connection DTOs
// ============================================================================

/// <summary>
/// A connection state change event.
/// </summary>
public record ConnectionEventDto
{
    /// <summary>
    /// Gets or sets the type of connection event (Connected, Disconnected, Connecting).
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the event occurred (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the reason for the event (e.g., exception message on disconnect).
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Gets or sets additional details about the event.
    /// </summary>
    public string? Details { get; set; }
}

/// <summary>
/// Aggregate statistics about connection events over a time period.
/// </summary>
public record ConnectionStatsDto
{
    /// <summary>
    /// Gets or sets the total number of connection events recorded.
    /// </summary>
    public int TotalEvents { get; set; }

    /// <summary>
    /// Gets or sets the number of reconnection events.
    /// </summary>
    public int ReconnectionCount { get; set; }

    /// <summary>
    /// Gets or sets the average session duration.
    /// </summary>
    public TimeSpan AverageSessionDuration { get; set; }

    /// <summary>
    /// Gets or sets the uptime percentage over the specified period.
    /// </summary>
    public double UptimePercentage { get; set; }
}

// ============================================================================
// Command Performance DTOs
// ============================================================================

/// <summary>
/// Aggregated performance metrics for a command.
/// </summary>
public record CommandPerformanceAggregateDto
{
    /// <summary>
    /// Gets or sets the command name.
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of times this command was executed.
    /// </summary>
    public int ExecutionCount { get; set; }

    /// <summary>
    /// Gets or sets the average execution time in milliseconds.
    /// </summary>
    public double AvgMs { get; set; }

    /// <summary>
    /// Gets or sets the minimum execution time in milliseconds.
    /// </summary>
    public double MinMs { get; set; }

    /// <summary>
    /// Gets or sets the maximum execution time in milliseconds.
    /// </summary>
    public double MaxMs { get; set; }

    /// <summary>
    /// Gets or sets the 50th percentile execution time in milliseconds.
    /// </summary>
    public double P50Ms { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile execution time in milliseconds.
    /// </summary>
    public double P95Ms { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile execution time in milliseconds.
    /// </summary>
    public double P99Ms { get; set; }

    /// <summary>
    /// Gets or sets the error rate as a percentage (0-100).
    /// </summary>
    public double ErrorRate { get; set; }
}

/// <summary>
/// Details about a slow command execution.
/// </summary>
public record SlowestCommandDto
{
    /// <summary>
    /// Gets or sets the command name.
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the command was executed (UTC).
    /// </summary>
    public DateTime ExecutedAt { get; set; }

    /// <summary>
    /// Gets or sets the execution duration in milliseconds.
    /// </summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the Discord user ID who executed the command.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild ID where the command was executed.
    /// </summary>
    public ulong? GuildId { get; set; }
}

/// <summary>
/// Command execution throughput over time.
/// </summary>
public record CommandThroughputDto
{
    /// <summary>
    /// Gets or sets the timestamp for this throughput measurement (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the number of commands executed in this time bucket.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the time granularity (hour, day).
    /// </summary>
    public string Granularity { get; set; } = string.Empty;
}

/// <summary>
/// Error breakdown for a command.
/// </summary>
public record CommandErrorBreakdownDto
{
    /// <summary>
    /// Gets or sets the command name.
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of errors for this command.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the collection of error messages and their frequency.
    /// </summary>
    public IReadOnlyDictionary<string, int> ErrorMessages { get; set; } = new Dictionary<string, int>();
}

// ============================================================================
// API Tracking DTOs
// ============================================================================

/// <summary>
/// Discord API usage statistics by category.
/// </summary>
public record ApiUsageDto
{
    /// <summary>
    /// Gets or sets the API category (REST, Gateway, etc.).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of requests in this category.
    /// </summary>
    public long RequestCount { get; set; }

    /// <summary>
    /// Gets or sets the average latency in milliseconds for this category.
    /// </summary>
    public double AvgLatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the number of errors in this category.
    /// </summary>
    public long ErrorCount { get; set; }
}

/// <summary>
/// A Discord API rate limit event.
/// </summary>
public record RateLimitEventDto
{
    /// <summary>
    /// Gets or sets when the rate limit was hit (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the endpoint that was rate limited.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the retry-after duration in milliseconds.
    /// </summary>
    public int RetryAfterMs { get; set; }

    /// <summary>
    /// Gets or sets whether this was a global rate limit.
    /// </summary>
    public bool IsGlobal { get; set; }
}

// ============================================================================
// Database DTOs
// ============================================================================

/// <summary>
/// Database query performance metrics.
/// </summary>
public record DatabaseMetricsDto
{
    /// <summary>
    /// Gets or sets the total number of queries executed.
    /// </summary>
    public long TotalQueries { get; set; }

    /// <summary>
    /// Gets or sets the average query execution time in milliseconds.
    /// </summary>
    public double AvgQueryTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the number of slow queries (exceeding the threshold).
    /// </summary>
    public int SlowQueryCount { get; set; }

    /// <summary>
    /// Gets or sets the query execution time distribution histogram.
    /// Keys are bucket names (e.g., "0-10ms", "10-50ms"), values are counts.
    /// </summary>
    public IReadOnlyDictionary<string, int> QueryHistogram { get; set; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets or sets connection pool statistics (if available).
    /// </summary>
    public string? ConnectionPoolStats { get; set; }
}

/// <summary>
/// Details about a slow database query.
/// </summary>
public record SlowQueryDto
{
    /// <summary>
    /// Gets or sets when the query was executed (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the SQL command text.
    /// </summary>
    public string CommandText { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the query execution duration in milliseconds.
    /// </summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// Gets or sets the query parameters (if captured).
    /// </summary>
    public string? Parameters { get; set; }
}

// ============================================================================
// Background Services DTOs
// ============================================================================

/// <summary>
/// Health status of a background service.
/// </summary>
public record BackgroundServiceHealthDto
{
    /// <summary>
    /// Gets or sets the service name.
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the service status (Running, Stopped, Error).
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the last heartbeat timestamp (UTC).
    /// </summary>
    public DateTime? LastHeartbeat { get; set; }

    /// <summary>
    /// Gets or sets the last error message (if any).
    /// </summary>
    public string? LastError { get; set; }
}

/// <summary>
/// Cache hit/miss statistics for a key prefix.
/// </summary>
public record CacheStatisticsDto
{
    /// <summary>
    /// Gets or sets the cache key prefix.
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the number of cache hits.
    /// </summary>
    public long Hits { get; set; }

    /// <summary>
    /// Gets or sets the number of cache misses.
    /// </summary>
    public long Misses { get; set; }

    /// <summary>
    /// Gets or sets the cache hit rate as a percentage (0-100).
    /// </summary>
    public double HitRate { get; set; }

    /// <summary>
    /// Gets or sets the approximate number of items in the cache for this prefix.
    /// </summary>
    public int Size { get; set; }
}

// ============================================================================
// Response Wrapper DTOs
// ============================================================================

/// <summary>
/// Connection history data with events and aggregate statistics.
/// </summary>
public record ConnectionHistoryDto
{
    /// <summary>
    /// Gets or sets the collection of connection events.
    /// </summary>
    public IReadOnlyList<ConnectionEventDto> Events { get; init; } = Array.Empty<ConnectionEventDto>();

    /// <summary>
    /// Gets or sets the aggregate statistics for the connection events.
    /// </summary>
    public ConnectionStatsDto Statistics { get; init; } = new();
}

/// <summary>
/// Command error summary with error rate, breakdown, and recent errors.
/// </summary>
public record CommandErrorsDto
{
    /// <summary>
    /// Gets or sets the overall error rate as a percentage (0-100).
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// Gets or sets the error breakdown by command.
    /// </summary>
    public IReadOnlyList<CommandErrorBreakdownDto> ByType { get; init; } = Array.Empty<CommandErrorBreakdownDto>();

    /// <summary>
    /// Gets or sets the most recent command errors.
    /// </summary>
    public IReadOnlyList<RecentCommandErrorDto> RecentErrors { get; init; } = Array.Empty<RecentCommandErrorDto>();
}

/// <summary>
/// Details about a recent command error.
/// </summary>
public record RecentCommandErrorDto
{
    /// <summary>
    /// Gets or sets when the error occurred (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the command name that failed.
    /// </summary>
    public string CommandName { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets or sets the guild ID where the error occurred.
    /// </summary>
    public ulong? GuildId { get; init; }
}

/// <summary>
/// API usage summary with total requests, breakdown by category, and rate limit hits.
/// </summary>
public record ApiUsageSummaryDto
{
    /// <summary>
    /// Gets or sets the total number of API requests.
    /// </summary>
    public long TotalRequests { get; init; }

    /// <summary>
    /// Gets or sets the API usage breakdown by category.
    /// </summary>
    public IReadOnlyList<ApiUsageDto> ByCategory { get; init; } = Array.Empty<ApiUsageDto>();

    /// <summary>
    /// Gets or sets the number of rate limit hits.
    /// </summary>
    public int RateLimitHits { get; init; }
}

/// <summary>
/// Rate limit summary with hit count and events.
/// </summary>
public record RateLimitSummaryDto
{
    /// <summary>
    /// Gets or sets the total number of rate limit hits.
    /// </summary>
    public int HitCount { get; init; }

    /// <summary>
    /// Gets or sets the collection of rate limit events.
    /// </summary>
    public IReadOnlyList<RateLimitEventDto> Events { get; init; } = Array.Empty<RateLimitEventDto>();
}

/// <summary>
/// Database metrics summary with overall metrics and recent slow queries.
/// </summary>
public record DatabaseMetricsSummaryDto
{
    /// <summary>
    /// Gets or sets the overall database metrics.
    /// </summary>
    public DatabaseMetricsDto Metrics { get; init; } = new();

    /// <summary>
    /// Gets or sets the collection of recent slow queries.
    /// </summary>
    public IReadOnlyList<SlowQueryDto> RecentSlowQueries { get; init; } = Array.Empty<SlowQueryDto>();
}

/// <summary>
/// Cache statistics summary with overall stats and breakdown by type.
/// </summary>
public record CacheSummaryDto
{
    /// <summary>
    /// Gets or sets the overall cache statistics across all prefixes.
    /// </summary>
    public CacheStatisticsDto Overall { get; init; } = new();

    /// <summary>
    /// Gets or sets the cache statistics breakdown by key prefix.
    /// </summary>
    public IReadOnlyList<CacheStatisticsDto> ByType { get; init; } = Array.Empty<CacheStatisticsDto>();
}
