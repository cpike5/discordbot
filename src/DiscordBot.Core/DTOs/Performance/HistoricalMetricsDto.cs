namespace DiscordBot.Core.DTOs;

/// <summary>
/// Response for historical system metrics endpoint with time range and aggregation info.
/// </summary>
public record HistoricalMetricsResponseDto
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
    /// Gets or sets the data granularity (e.g., "raw", "5m", "15m", "1h").
    /// </summary>
    public string Granularity { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection of metric snapshots.
    /// </summary>
    public IReadOnlyList<MetricSnapshotDto> Snapshots { get; init; } = Array.Empty<MetricSnapshotDto>();
}
