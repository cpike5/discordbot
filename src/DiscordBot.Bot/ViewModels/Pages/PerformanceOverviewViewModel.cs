using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Performance Overview page, displaying aggregated performance metrics and system health.
/// </summary>
public record PerformanceOverviewViewModel
{
    /// <summary>
    /// Gets the overall health status (Healthy, Warning, Critical).
    /// </summary>
    public string OverallStatus { get; init; } = "Healthy";

    /// <summary>
    /// Gets the bot health status.
    /// </summary>
    public PerformanceHealthDto BotHealth { get; init; } = new();

    /// <summary>
    /// Gets the uptime percentage for the last 30 days.
    /// </summary>
    public double Uptime30DaysPercent { get; init; }

    /// <summary>
    /// Gets the formatted uptime percentage for display.
    /// </summary>
    public string Uptime30DaysFormatted => $"{Uptime30DaysPercent:F1}%";

    /// <summary>
    /// Gets the average command response time in milliseconds.
    /// </summary>
    public double AvgCommandResponseMs { get; init; }

    /// <summary>
    /// Gets the number of commands executed in the selected time range.
    /// </summary>
    public int CommandsInRange { get; init; }

    /// <summary>
    /// Gets the selected time range in hours (24, 168 or 720).
    /// </summary>
    public int TimeRangeHours { get; init; } = 24;

    /// <summary>
    /// Gets the time range as words for captions ("Last 24 hours", "Last 7 days", "Last 30 days").
    /// </summary>
    public string TimeRangeCaption => TimeRangeHours switch
    {
        168 => "Last 7 days",
        720 => "Last 30 days",
        _ => "Last 24 hours"
    };

    /// <summary>
    /// Gets a value indicating whether the overview data could not be loaded. The numbers are then
    /// placeholders, and the tab says so instead of presenting them as real.
    /// </summary>
    public bool LoadFailed { get; init; }

    /// <summary>
    /// Gets the overall error rate percentage.
    /// </summary>
    public double ErrorRate { get; init; }

    /// <summary>
    /// Gets the formatted error rate for display.
    /// </summary>
    public string ErrorRateFormatted => $"{ErrorRate:F1}%";

    /// <summary>
    /// Gets the number of active performance alerts.
    /// </summary>
    public int ActiveAlertCount { get; init; }

    /// <summary>
    /// Gets the list of recent active alerts (3-5 most recent).
    /// </summary>
    public IReadOnlyList<PerformanceIncidentDto> RecentAlerts { get; init; } = Array.Empty<PerformanceIncidentDto>();

    /// <summary>
    /// Gets the memory usage in MB.
    /// </summary>
    public long MemoryUsageMB { get; init; }

    /// <summary>
    /// Gets the memory usage percentage (0-100).
    /// </summary>
    public double MemoryUsagePercent { get; init; }

    /// <summary>
    /// Gets the formatted memory usage string (e.g., "256 MB / 512 MB").
    /// </summary>
    public string MemoryUsageFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the CPU usage percentage.
    /// </summary>
    public double CpuUsagePercent { get; init; }

    /// <summary>
    /// Gets the API rate limit usage (e.g., "45 / 50 requests").
    /// </summary>
    public string ApiRateLimitFormatted { get; init; } = string.Empty;

    /// <summary>
    /// Gets the API rate limit percentage (0-100).
    /// </summary>
    public double ApiRateLimitPercent { get; init; }

    /// <summary>
    /// Gets the CSS class for overall health status badge.
    /// </summary>
    public string OverallStatusClass => OverallStatus.ToLowerInvariant() switch
    {
        "healthy" => "health-status-healthy",
        "warning" => "health-status-warning",
        "critical" => "health-status-error",
        _ => "health-status-healthy"
    };

    /// <summary>
    /// Gets the display text for overall health status badge.
    /// </summary>
    public string OverallStatusText => OverallStatus.ToLowerInvariant() switch
    {
        "healthy" => "Operational",
        "warning" => "Degraded",
        "critical" => "Critical",
        _ => "Operational"
    };

    /// <summary>
    /// Gets the CSS class for bot health status.
    /// </summary>
    public string BotHealthStatusClass => BotHealth.ConnectionState.ToLowerInvariant() switch
    {
        // The gateway state decides; a degraded background service tempers a connected bot.
        "connected" => BotHealth.Status.ToLowerInvariant() switch
        {
            "warning" => "text-warning",
            "critical" => "text-error",
            _ => "text-success"
        },
        "connecting" => "text-warning",
        _ => "text-error"
    };

    /// <summary>
    /// Gets the bot health status text.
    /// </summary>
    public string BotHealthStatusText => BotHealth.ConnectionState.ToLowerInvariant() switch
    {
        "connected" => "Healthy",
        "connecting" => "Connecting",
        _ => "Disconnected"
    };

    /// <summary>
    /// Gets the system status word, from the worse of memory and CPU use.
    /// </summary>
    public string SystemStatusText => Math.Max(MemoryUsagePercent, CpuUsagePercent) switch
    {
        >= 90 => "Critical",
        >= 75 => "Elevated",
        _ => "Normal"
    };

    /// <summary>
    /// Gets the text colour class for <see cref="SystemStatusText"/>.
    /// </summary>
    public string SystemStatusClass => SystemStatusText switch
    {
        "Critical" => "text-error",
        "Elevated" => "text-warning",
        _ => "text-success"
    };

    /// <summary>
    /// Gets the text colour class for the API status card: amber once Discord has rate limited the bot.
    /// </summary>
    public string ApiStatusClass => ApiRateLimitPercent > 0 ? "text-warning" : "text-success";

    /// <summary>
    /// Gets the average latency in milliseconds.
    /// </summary>
    public int AvgLatencyMs => BotHealth.LatencyMs;

    /// <summary>
    /// Gets the CSS class for memory usage progress bar.
    /// </summary>
    public string MemoryUsageProgressClass => MemoryUsagePercent switch
    {
        < 60 => "progress-bar-healthy",
        < 80 => "progress-bar-warning",
        _ => "progress-bar-error"
    };

    /// <summary>
    /// Gets the CSS class for API rate limit progress bar.
    /// </summary>
    public string ApiRateLimitProgressClass => ApiRateLimitPercent switch
    {
        < 70 => "progress-bar-healthy",
        < 90 => "progress-bar-warning",
        _ => "progress-bar-error"
    };
}
