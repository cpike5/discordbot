namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the Performance pages navigation tabs component.
/// </summary>
public class PerformanceTabsViewModel
{
    /// <summary>
    /// The currently active tab identifier.
    /// Valid values: "overview", "health", "commands", "api", "system", "alerts"
    /// </summary>
    public string ActiveTab { get; set; } = "overview";

    /// <summary>
    /// Optional: Number of active alerts to display as a badge on the Alerts tab.
    /// </summary>
    public int ActiveAlertCount { get; set; }

    /// <summary>
    /// When true, tabs use AJAX navigation to load content without page reload.
    /// When false (default), tabs link to separate pages.
    /// </summary>
    public bool UseAjaxNavigation { get; set; }
}
