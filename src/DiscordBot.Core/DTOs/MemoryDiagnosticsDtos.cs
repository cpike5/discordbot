namespace DiscordBot.Core.DTOs;

/// <summary>
/// Memory usage report for a single service.
/// </summary>
public record ServiceMemoryReportDto
{
    /// <summary>
    /// Gets the display name of the service.
    /// </summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the estimated memory usage in bytes.
    /// </summary>
    public long EstimatedBytes { get; init; }

    /// <summary>
    /// Gets the number of items/entries being tracked by this service.
    /// </summary>
    public int ItemCount { get; init; }

    /// <summary>
    /// Gets additional details about memory usage (e.g., buffer occupancy, configuration).
    /// </summary>
    public string? Details { get; init; }
}

/// <summary>
/// GC heap generation sizes and fragmentation information.
/// </summary>
public record GcGenerationSizesDto
{
    /// <summary>
    /// Gets the Generation 0 heap size in bytes.
    /// </summary>
    public long Gen0SizeBytes { get; init; }

    /// <summary>
    /// Gets the Generation 1 heap size in bytes.
    /// </summary>
    public long Gen1SizeBytes { get; init; }

    /// <summary>
    /// Gets the Generation 2 heap size in bytes.
    /// </summary>
    public long Gen2SizeBytes { get; init; }

    /// <summary>
    /// Gets the Large Object Heap (LOH) size in bytes.
    /// </summary>
    public long LohSizeBytes { get; init; }

    /// <summary>
    /// Gets the Pinned Object Heap (POH) size in bytes (.NET 5+).
    /// </summary>
    public long PohSizeBytes { get; init; }

    /// <summary>
    /// Gets the total committed memory in bytes.
    /// </summary>
    public long TotalCommittedBytes { get; init; }

    /// <summary>
    /// Gets the heap fragmentation percentage (0-100).
    /// </summary>
    public double FragmentationPercent { get; init; }
}

/// <summary>
/// Complete memory diagnostics including GC info and service breakdown.
/// </summary>
public record MemoryDiagnosticsDto
{
    /// <summary>
    /// Gets the timestamp when this report was generated (UTC).
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets the GC generation sizes and fragmentation info.
    /// </summary>
    public GcGenerationSizesDto GcGenerations { get; init; } = new();

    /// <summary>
    /// Gets the collection of service memory reports, ordered by size descending.
    /// </summary>
    public IReadOnlyList<ServiceMemoryReportDto> ServiceReports { get; init; } = Array.Empty<ServiceMemoryReportDto>();

    /// <summary>
    /// Gets the total accounted memory from all services in bytes.
    /// </summary>
    public long TotalAccountedBytes { get; init; }

    /// <summary>
    /// Gets the unaccounted memory in bytes (committed - accounted).
    /// This includes framework overhead, native memory, and services not yet instrumented.
    /// </summary>
    public long UnaccountedBytes { get; init; }

    /// <summary>
    /// Gets the working set memory in bytes (total physical memory used).
    /// </summary>
    public long WorkingSetBytes { get; init; }

    /// <summary>
    /// Gets the managed heap size in bytes (via GC.GetTotalMemory).
    /// </summary>
    public long ManagedHeapBytes { get; init; }
}
