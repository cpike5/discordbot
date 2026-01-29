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
    /// Uses outline icons for all states for consistent visual weight.
    /// </summary>
    public static IReadOnlyList<TabDefinition> Tabs { get; } = new List<TabDefinition>
    {
        new("overview", "Overview", "overview",
            // Outline: chart-bar
            "M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z",
            // Solid: chart-bar
            "M2 11a1 1 0 011-1h2a1 1 0 011 1v5a1 1 0 01-1 1H3a1 1 0 01-1-1v-5zm6-4a1 1 0 011-1h2a1 1 0 011 1v9a1 1 0 01-1 1H9a1 1 0 01-1-1V7zm6-3a1 1 0 011-1h2a1 1 0 011 1v12a1 1 0 01-1 1h-2a1 1 0 01-1-1V4z"),
        new("health", "Health Metrics", "health-metrics",
            // Outline: heart
            "M4.318 6.318a4.5 4.5 0 000 6.364L12 20.364l7.682-7.682a4.5 4.5 0 00-6.364-6.364L12 7.636l-1.318-1.318a4.5 4.5 0 00-6.364 0z",
            // Solid: heart
            "M3.172 5.172a4 4 0 015.656 0L12 8.343l3.172-3.171a4 4 0 115.656 5.656L12 19.657l-8.828-8.829a4 4 0 010-5.656z",
            "Health"),
        new("commands", "Commands", "commands",
            // Outline: lightning-bolt
            "M13 10V3L4 14h7v7l9-11h-7z",
            // Solid: lightning-bolt (same path works as solid with fill)
            "M11.3 1.046A1 1 0 0112 2v5h4a1 1 0 01.82 1.573l-7 10A1 1 0 018 18v-5H4a1 1 0 01-.82-1.573l7-10a1 1 0 011.12-.38z"),
        new("api", "API & Rate Limits", "api-metrics",
            // Outline: terminal
            "M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z",
            // Solid: terminal (code-bracket-square)
            "M14.447 3.027a.75.75 0 01.527.92l-4.5 16.5a.75.75 0 01-1.448-.394l4.5-16.5a.75.75 0 01.921-.526zM16.72 6.22a.75.75 0 011.06 0l5.25 5.25a.75.75 0 010 1.06l-5.25 5.25a.75.75 0 11-1.06-1.06L21.44 12l-4.72-4.72a.75.75 0 010-1.06zm-9.44 0a.75.75 0 010 1.06L2.56 12l4.72 4.72a.75.75 0 11-1.06 1.06L.97 12.53a.75.75 0 010-1.06l5.25-5.25a.75.75 0 011.06 0z",
            "API"),
        new("system", "System", "system-health",
            // Outline: server
            "M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01",
            // Solid: server
            "M4.5 3A1.5 1.5 0 003 4.5v4A1.5 1.5 0 004.5 10h11a1.5 1.5 0 001.5-1.5v-4A1.5 1.5 0 0015.5 3h-11zm0 11A1.5 1.5 0 003 15.5v4A1.5 1.5 0 004.5 21h11a1.5 1.5 0 001.5-1.5v-4a1.5 1.5 0 00-1.5-1.5h-11zM13 7a1 1 0 100-2 1 1 0 000 2zm-3 0a1 1 0 100-2 1 1 0 000 2zm6 11a1 1 0 100-2 1 1 0 000 2zm-3 0a1 1 0 100-2 1 1 0 000 2z"),
        new("alerts", "Alerts", "alerts",
            // Outline: bell
            "M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9",
            // Solid: bell
            "M10 2a6 6 0 00-6 6v3.586l-.707.707A1 1 0 004 14h12a1 1 0 00.707-1.707L16 11.586V8a6 6 0 00-6-6zM10 18a3 3 0 01-3-3h6a3 3 0 01-3 3z")
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
/// <param name="IconPath">The SVG path for the tab icon (outline version, used for all states).</param>
/// <param name="IconPathSolid">Legacy parameter - kept for backward compatibility but not used.</param>
/// <param name="ShortLabel">Optional short label for mobile display.</param>
[Obsolete("IconPathSolid parameter is deprecated. Use IconPath for all states.")]
public record TabDefinition(
    string Id,
    string Label,
    string Hash,
    string IconPath,
    string IconPathSolid,
    string? ShortLabel = null);
