namespace DiscordBot.Bot.ViewModels.Pages.Performance;

/// <summary>
/// Marker interface for Performance Dashboard tab view models.
/// Enables consistent partial view rendering and time range filtering.
/// </summary>
public interface IPerformanceTabViewModel
{
    /// <summary>
    /// Gets the tab identifier used for routing and active tab detection.
    /// </summary>
    string TabId { get; }

    /// <summary>
    /// Gets the time range in hours for data filtering (default: 24).
    /// </summary>
    int Hours { get; }
}
