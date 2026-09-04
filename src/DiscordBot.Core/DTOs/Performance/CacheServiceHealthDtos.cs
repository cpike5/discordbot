namespace DiscordBot.Core.DTOs;

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

// ============================================================================
// Historical Metrics DTOs
// ============================================================================
