namespace DiscordBot.Core.DTOs;

/// <summary>
/// CPU history data with samples and statistical analysis.
/// </summary>
public record CpuHistoryDto
{
    /// <summary>
    /// Gets or sets the collection of CPU usage samples.
    /// </summary>
    public IReadOnlyList<CpuSampleDto> Samples { get; set; } = Array.Empty<CpuSampleDto>();

    /// <summary>
    /// Gets or sets the statistical summary of the CPU usage data.
    /// </summary>
    public CpuStatisticsDto Statistics { get; set; } = new();
}

/// <summary>
/// A single CPU usage measurement sample.
/// </summary>
public record CpuSampleDto
{
    /// <summary>
    /// Gets or sets the timestamp when the sample was recorded (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the CPU usage percentage (0-100).
    /// </summary>
    public double CpuPercent { get; set; }
}

/// <summary>
/// Statistical analysis of CPU usage samples.
/// </summary>
public record CpuStatisticsDto
{
    /// <summary>
    /// Gets or sets the average CPU usage percentage.
    /// </summary>
    public double Average { get; set; }

    /// <summary>
    /// Gets or sets the minimum CPU usage percentage.
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// Gets or sets the maximum CPU usage percentage.
    /// </summary>
    public double Max { get; set; }

    /// <summary>
    /// Gets or sets the 50th percentile (median) CPU usage percentage.
    /// </summary>
    public double P50 { get; set; }

    /// <summary>
    /// Gets or sets the 95th percentile CPU usage percentage.
    /// </summary>
    public double P95 { get; set; }

    /// <summary>
    /// Gets or sets the 99th percentile CPU usage percentage.
    /// </summary>
    public double P99 { get; set; }

    /// <summary>
    /// Gets or sets the total number of samples included in this statistical analysis.
    /// </summary>
    public int SampleCount { get; set; }
}

// ============================================================================
// Connection DTOs
// ============================================================================

/// <summary>
/// A single historical memory metrics sample.
/// </summary>
public record MemoryHistorySampleDto
{
    /// <summary>
    /// Gets or sets the timestamp of the sample (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets or sets the working set memory in MB.
    /// </summary>
    public long WorkingSetMB { get; init; }

    /// <summary>
    /// Gets or sets the heap size in MB.
    /// </summary>
    public long HeapSizeMB { get; init; }

    /// <summary>
    /// Gets or sets the private memory in MB.
    /// </summary>
    public long PrivateMemoryMB { get; init; }
}

/// <summary>
/// Statistics for memory metrics over the requested time range.
/// </summary>
public record MemoryHistoryStatisticsDto
{
    /// <summary>
    /// Gets or sets the average working set memory in MB.
    /// </summary>
    public double AvgWorkingSetMB { get; init; }

    /// <summary>
    /// Gets or sets the maximum working set memory in MB.
    /// </summary>
    public long MaxWorkingSetMB { get; init; }

    /// <summary>
    /// Gets or sets the average heap size in MB.
    /// </summary>
    public double AvgHeapSizeMB { get; init; }
}

/// <summary>
/// Response for historical memory metrics endpoint.
/// </summary>
public record MemoryHistoryResponseDto
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
    /// Gets or sets the collection of memory metrics samples.
    /// </summary>
    public IReadOnlyList<MemoryHistorySampleDto> Samples { get; init; } = Array.Empty<MemoryHistorySampleDto>();

    /// <summary>
    /// Gets or sets the aggregate statistics for the time range.
    /// </summary>
    public MemoryHistoryStatisticsDto Statistics { get; init; } = new();
}
