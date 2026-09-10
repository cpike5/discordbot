namespace DiscordBot.Core.DTOs;

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
/// A single historical database metrics sample.
/// </summary>
public record DatabaseHistorySampleDto
{
    /// <summary>
    /// Gets or sets the timestamp of the sample (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the average query time in milliseconds.
    /// </summary>
    public double AvgQueryTimeMs { get; init; }

    /// <summary>
    /// Gets or sets the total queries at this snapshot.
    /// </summary>
    public long TotalQueries { get; init; }

    /// <summary>
    /// Gets or sets the slow query count at this snapshot.
    /// </summary>
    public int SlowQueryCount { get; init; }
}

/// <summary>
/// Statistics for database metrics over the requested time range.
/// </summary>
public record DatabaseHistoryStatisticsDto
{
    /// <summary>
    /// Gets or sets the average query time across all samples.
    /// </summary>
    public double AvgQueryTimeMs { get; init; }

    /// <summary>
    /// Gets or sets the minimum query time observed.
    /// </summary>
    public double MinQueryTimeMs { get; init; }

    /// <summary>
    /// Gets or sets the maximum query time observed.
    /// </summary>
    public double MaxQueryTimeMs { get; init; }

    /// <summary>
    /// Gets or sets the total slow queries across all samples.
    /// </summary>
    public int TotalSlowQueries { get; init; }
}

/// <summary>
/// Response for historical database metrics endpoint.
/// </summary>
public record DatabaseHistoryResponseDto
{
    /// <summary>
    /// Gets or sets the start time of the data range (UTC).
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// Gets or sets the end time of the data range (UTC).
    /// </summary>
    public DateTime EndTime { get; init; }

    /// <summary>
    /// Gets or sets the collection of database metrics samples.
    /// </summary>
    public IReadOnlyList<DatabaseHistorySampleDto> Samples { get; init; } = Array.Empty<DatabaseHistorySampleDto>();

    /// <summary>
    /// Gets or sets the aggregate statistics for the time range.
    /// </summary>
    public DatabaseHistoryStatisticsDto Statistics { get; init; } = new();
}
