namespace DiscordBot.Core.DTOs;

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
/// Hourly API request volume for charting.
/// </summary>
public record ApiRequestVolumeDto
{
    /// <summary>
    /// Gets or sets the timestamp for the hour bucket.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the number of requests in this hour.
    /// </summary>
    public long RequestCount { get; init; }

    /// <summary>
    /// Gets or sets the category of requests (REST, Gateway, etc.).
    /// </summary>
    public string Category { get; init; } = string.Empty;
}

/// <summary>
/// API latency sample for time series charting.
/// </summary>
public record ApiLatencySampleDto
{
    /// <summary>
    /// Gets or sets the timestamp of the sample.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the average latency in milliseconds.
    /// </summary>
    public double AvgLatencyMs { get; init; }

    /// <summary>
    /// Gets or sets the 95th percentile latency.
    /// </summary>
    public double P95LatencyMs { get; init; }
}

/// <summary>
/// Complete API latency statistics.
/// </summary>
public record ApiLatencyStatsDto
{
    /// <summary>
    /// Gets or sets the average latency in milliseconds.
    /// </summary>
    public double AvgLatencyMs { get; init; }

    /// <summary>
    /// Gets or sets the minimum latency observed.
    /// </summary>
    public double MinLatencyMs { get; init; }

    /// <summary>
    /// Gets or sets the maximum latency observed.
    /// </summary>
    public double MaxLatencyMs { get; init; }

    /// <summary>
    /// Gets or sets the 50th percentile (median) latency.
    /// </summary>
    public double P50LatencyMs { get; init; }

    /// <summary>
    /// Gets or sets the 95th percentile latency.
    /// </summary>
    public double P95LatencyMs { get; init; }

    /// <summary>
    /// Gets or sets the 99th percentile latency.
    /// </summary>
    public double P99LatencyMs { get; init; }

    /// <summary>
    /// Gets or sets the number of samples used for statistics.
    /// </summary>
    public int SampleCount { get; init; }
}

/// <summary>
/// Full API latency history with samples and statistics.
/// </summary>
public record ApiLatencyHistoryDto
{
    /// <summary>
    /// Gets or sets the time series samples for charting.
    /// </summary>
    public IReadOnlyList<ApiLatencySampleDto> Samples { get; init; } = Array.Empty<ApiLatencySampleDto>();

    /// <summary>
    /// Gets or sets the aggregate statistics for the period.
    /// </summary>
    public ApiLatencyStatsDto Statistics { get; init; } = new();
}
