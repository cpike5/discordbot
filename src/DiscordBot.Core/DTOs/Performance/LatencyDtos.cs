namespace DiscordBot.Core.DTOs;

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
// CPU History DTOs
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
