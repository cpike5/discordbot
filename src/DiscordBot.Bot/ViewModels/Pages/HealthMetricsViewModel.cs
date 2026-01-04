using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Health Metrics page, displaying bot health, uptime, and connection status.
/// </summary>
public record HealthMetricsViewModel
{
    /// <summary>
    /// Gets the detailed memory diagnostics including GC and service breakdown.
    /// </summary>
    public MemoryDiagnosticsDto? MemoryDiagnostics { get; init; }

    /// <summary>
    /// Gets the current health status of the bot.
    /// </summary>
    public PerformanceHealthDto Health { get; init; } = new();

    /// <summary>
    /// Gets the latency statistics for the current time range.
    /// </summary>
    public LatencyStatisticsDto LatencyStats { get; init; } = new();

    /// <summary>
    /// Gets the connection statistics.
    /// </summary>
    public ConnectionStatsDto ConnectionStats { get; init; } = new();

    /// <summary>
    /// Gets the recent connection events.
    /// </summary>
    public IReadOnlyList<ConnectionEventDto> RecentConnectionEvents { get; init; } = Array.Empty<ConnectionEventDto>();

    /// <summary>
    /// Gets the formatted uptime string for the current session.
    /// </summary>
    public string UptimeFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the formatted uptime percentage for 24 hours.
    /// </summary>
    public string Uptime24HFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the formatted uptime percentage for 7 days.
    /// </summary>
    public string Uptime7DFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the formatted uptime percentage for 30 days.
    /// </summary>
    public string Uptime30DFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the connection state CSS class for styling.
    /// </summary>
    public string ConnectionStateClass { get; init; } = string.Empty;

    /// <summary>
    /// Gets the latency health status CSS class.
    /// </summary>
    public string LatencyHealthClass { get; init; } = string.Empty;

    /// <summary>
    /// Gets the formatted start time of the current session (UTC fallback text).
    /// </summary>
    public string SessionStartFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC start time of the current session for client-side conversion.
    /// </summary>
    public DateTime? SessionStartUtc { get; init; }

    /// <summary>
    /// Gets the current working set memory in MB.
    /// </summary>
    public long WorkingSetMB { get; init; }

    /// <summary>
    /// Gets the private memory in MB.
    /// </summary>
    public long PrivateMemoryMB { get; init; }

    /// <summary>
    /// Gets the maximum allocated memory in MB (GC TotalMemory).
    /// </summary>
    public long MaxAllocatedMemoryMB { get; init; }

    /// <summary>
    /// Gets the memory utilization percentage (WorkingSet / MaxAllocated * 100).
    /// </summary>
    public double MemoryUtilizationPercent { get; init; }

    /// <summary>
    /// Gets the number of Gen 2 garbage collections.
    /// </summary>
    public int Gen2Collections { get; init; }

    /// <summary>
    /// Gets the current CPU usage percentage.
    /// </summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>
    /// Gets the thread count.
    /// </summary>
    public int ThreadCount { get; init; }

    /// <summary>
    /// Gets the recent latency samples for sparkline visualization (last 10 samples).
    /// </summary>
    public IReadOnlyList<LatencySampleDto> RecentLatencySamples { get; init; } = Array.Empty<LatencySampleDto>();

    /// <summary>
    /// Formats a TimeSpan into a human-readable uptime string (e.g., "16d 8h 30m").
    /// </summary>
    public static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }

        if (uptime.TotalHours >= 1)
        {
            return $"{uptime.Hours}h {uptime.Minutes}m";
        }

        return $"{uptime.Minutes}m";
    }

    /// <summary>
    /// Gets the CSS class for connection state styling.
    /// </summary>
    public static string GetConnectionStateClass(string connectionState)
    {
        return connectionState.ToUpperInvariant() switch
        {
            "CONNECTED" => "health-status-healthy",
            "CONNECTING" => "health-status-warning",
            _ => "health-status-error"
        };
    }

    /// <summary>
    /// Gets the CSS class for latency health styling.
    /// </summary>
    public static string GetLatencyHealthClass(int latencyMs)
    {
        return latencyMs switch
        {
            < 100 => "gauge-fill-healthy",
            < 200 => "gauge-fill-warning",
            _ => "gauge-fill-error"
        };
    }

    /// <summary>
    /// Formats bytes to a human-readable string with appropriate unit.
    /// </summary>
    /// <param name="bytes">The number of bytes to format.</param>
    /// <returns>A formatted string like "1.5 GB", "256.3 MB", "64.0 KB", or "512 B".</returns>
    public static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        if (bytes >= 1024L * 1024L)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        if (bytes >= 1024L)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }

    /// <summary>
    /// Gets the CSS class for fragmentation percentage styling.
    /// </summary>
    /// <param name="fragmentationPercent">The fragmentation percentage (0-100).</param>
    /// <returns>A CSS class name for text coloring.</returns>
    public static string GetFragmentationClass(double fragmentationPercent)
    {
        return fragmentationPercent switch
        {
            < 10 => "text-success",
            < 25 => "text-warning",
            _ => "text-error"
        };
    }
}
