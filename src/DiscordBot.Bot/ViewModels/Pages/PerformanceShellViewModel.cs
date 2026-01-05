namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// ViewModel for the Performance Dashboard shell layout.
/// Contains shared state for all performance tabs including header, time range, and overall health.
/// </summary>
public record PerformanceShellViewModel
{
    /// <summary>
    /// Gets the overall health status (Healthy, Warning, Critical).
    /// </summary>
    public string OverallStatus { get; init; } = "Healthy";

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
    /// Gets the number of active performance alerts.
    /// </summary>
    public int ActiveAlertCount { get; init; }

    /// <summary>
    /// Gets the currently active tab identifier.
    /// Valid values: "overview", "health", "commands", "api", "system", "alerts"
    /// </summary>
    public string ActiveTab { get; init; } = "overview";

    /// <summary>
    /// Gets the selected time range in hours (24, 168, or 720).
    /// </summary>
    public int TimeRangeHours { get; init; } = 24;

    /// <summary>
    /// Gets the display label for the selected time range.
    /// </summary>
    public string TimeRangeLabel => TimeRangeHours switch
    {
        24 => "24h",
        168 => "7d",
        720 => "30d",
        _ => "24h"
    };

    /// <summary>
    /// Gets whether the live data connection is active.
    /// </summary>
    public bool IsLive { get; init; } = true;

    /// <summary>
    /// Gets the tab definitions for navigation.
    /// </summary>
    public static IReadOnlyList<TabDefinition> Tabs { get; } = new List<TabDefinition>
    {
        new("overview", "Overview", "overview", "M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"),
        new("health", "Health Metrics", "health-metrics", "M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z", "Health"),
        new("commands", "Commands", "commands", "M13 10V3L4 14h7v7l9-11h-7z"),
        new("api", "API & Rate Limits", "api-metrics", "M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z", "API"),
        new("system", "System", "system-health", "M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01"),
        new("alerts", "Alerts", "alerts", "M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9")
    };

    /// <summary>
    /// Gets the tab definition for the currently active tab.
    /// </summary>
    public TabDefinition? CurrentTab => Tabs.FirstOrDefault(t => t.Id == ActiveTab);
}

/// <summary>
/// Represents a tab in the Performance Dashboard navigation.
/// </summary>
/// <param name="Id">The unique tab identifier.</param>
/// <param name="Label">The full tab label.</param>
/// <param name="Hash">The URL hash for this tab (without #).</param>
/// <param name="IconPath">The SVG path for the tab icon.</param>
/// <param name="ShortLabel">Optional short label for mobile display.</param>
public record TabDefinition(
    string Id,
    string Label,
    string Hash,
    string IconPath,
    string? ShortLabel = null);
