using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the System Health page, displaying database, cache, and background service metrics.
/// </summary>
public record SystemHealthViewModel
{
    /// <summary>
    /// Gets the database performance metrics.
    /// </summary>
    public DatabaseMetricsDto DatabaseMetrics { get; init; } = new();

    /// <summary>
    /// Gets the collection of recent slow database queries.
    /// </summary>
    public IReadOnlyList<SlowQueryDto> SlowQueries { get; init; } = Array.Empty<SlowQueryDto>();

    /// <summary>
    /// Gets the collection of background service health statuses.
    /// </summary>
    public IReadOnlyList<BackgroundServiceHealthDto> BackgroundServices { get; init; } = Array.Empty<BackgroundServiceHealthDto>();

    /// <summary>
    /// Gets the overall cache statistics.
    /// </summary>
    public CacheStatisticsDto OverallCacheStats { get; init; } = new();

    /// <summary>
    /// Gets the cache statistics breakdown by prefix.
    /// </summary>
    public IReadOnlyList<CacheStatisticsDto> CacheStatsByPrefix { get; init; } = Array.Empty<CacheStatisticsDto>();

    /// <summary>
    /// Gets the current working set memory in MB.
    /// </summary>
    public long WorkingSetMB { get; init; }

    /// <summary>
    /// Gets the private memory in MB.
    /// </summary>
    public long PrivateMemoryMB { get; init; }

    /// <summary>
    /// Gets the heap size in MB (GC TotalMemory).
    /// </summary>
    public long HeapSizeMB { get; init; }

    /// <summary>
    /// Gets the number of Gen 0 garbage collections.
    /// </summary>
    public int Gen0Collections { get; init; }

    /// <summary>
    /// Gets the number of Gen 1 garbage collections.
    /// </summary>
    public int Gen1Collections { get; init; }

    /// <summary>
    /// Gets the number of Gen 2 garbage collections.
    /// </summary>
    public int Gen2Collections { get; init; }

    /// <summary>
    /// Gets the overall system status (Healthy, Warning, Error).
    /// </summary>
    public string SystemStatus { get; init; } = string.Empty;

    /// <summary>
    /// Gets the system status CSS class for styling.
    /// </summary>
    public string SystemStatusClass { get; init; } = string.Empty;

    /// <summary>
    /// Gets the queries per second rate.
    /// </summary>
    public double QueriesPerSecond { get; init; }

    /// <summary>
    /// Gets the database error count.
    /// </summary>
    public int DatabaseErrorCount { get; init; }

    /// <summary>
    /// Gets the CSS class for database query time status.
    /// </summary>
    /// <param name="avgQueryTimeMs">Average query time in milliseconds.</param>
    /// <returns>CSS class name for styling.</returns>
    public static string GetQueryTimeStatusClass(double avgQueryTimeMs)
    {
        return avgQueryTimeMs switch
        {
            < 50 => "text-success",
            < 100 => "text-warning",
            _ => "text-error"
        };
    }

    /// <summary>
    /// Gets the CSS class for cache hit rate status.
    /// </summary>
    /// <param name="hitRate">Hit rate as a percentage (0-100).</param>
    /// <returns>CSS class name for styling.</returns>
    public static string GetCacheHitRateClass(double hitRate)
    {
        return hitRate switch
        {
            >= 90 => "progress-bar-healthy",
            >= 70 => "progress-bar-warning",
            _ => "progress-bar-error"
        };
    }

    /// <summary>
    /// Gets the CSS class for background service status.
    /// </summary>
    /// <param name="status">Service status string.</param>
    /// <returns>CSS class name for styling.</returns>
    public static string GetServiceStatusClass(string status)
    {
        return status.ToUpperInvariant() switch
        {
            "RUNNING" => "bg-success",
            "STARTING" => "bg-warning",
            "STOPPED" => "bg-border-primary",
            _ => "bg-error"
        };
    }

    /// <summary>
    /// Gets the overall system status based on service health and metrics.
    /// </summary>
    /// <param name="backgroundServices">Collection of background service health statuses.</param>
    /// <param name="avgQueryTimeMs">Average database query time in milliseconds.</param>
    /// <param name="errorCount">Database error count.</param>
    /// <returns>System status string (Healthy, Degraded, Error).</returns>
    public static string GetSystemStatus(
        IReadOnlyList<BackgroundServiceHealthDto> backgroundServices,
        double avgQueryTimeMs,
        int errorCount)
    {
        var hasErroredServices = backgroundServices.Any(s =>
            s.Status.Equals("Error", StringComparison.OrdinalIgnoreCase) ||
            s.Status.Equals("Stopped", StringComparison.OrdinalIgnoreCase));

        if (hasErroredServices || errorCount > 10 || avgQueryTimeMs > 200)
        {
            return "System Degraded";
        }

        if (errorCount > 0 || avgQueryTimeMs > 100)
        {
            return "All Services Running";
        }

        return "All Services Healthy";
    }

    /// <summary>
    /// Gets the CSS class for system status badge.
    /// </summary>
    /// <param name="systemStatus">System status string.</param>
    /// <returns>CSS class name for styling.</returns>
    public static string GetSystemStatusClass(string systemStatus)
    {
        if (systemStatus.Contains("Degraded", StringComparison.OrdinalIgnoreCase))
        {
            return "health-status-error";
        }

        if (systemStatus.Contains("Running", StringComparison.OrdinalIgnoreCase))
        {
            return "health-status-warning";
        }

        return "health-status-healthy";
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string with MB or KB suffix.
    /// </summary>
    /// <param name="bytes">Size in bytes.</param>
    /// <returns>Formatted string with appropriate unit.</returns>
    public static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
        {
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        if (bytes >= 1024)
        {
            return $"{bytes / 1024.0:F0} KB";
        }

        return $"{bytes} B";
    }

    /// <summary>
    /// Formats a duration from milliseconds to a human-readable string.
    /// </summary>
    /// <param name="durationMs">Duration in milliseconds.</param>
    /// <returns>Formatted string with appropriate unit.</returns>
    public static string FormatDuration(double durationMs)
    {
        if (durationMs >= 1000)
        {
            return $"{durationMs / 1000.0:F2}s";
        }

        return $"{durationMs:F0}ms";
    }
}
