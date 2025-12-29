namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// Represents the view model for the activity feed timeline component.
/// </summary>
public class ActivityFeedTimelineViewModel
{
    /// <summary>
    /// Gets or sets the title displayed in the card header.
    /// </summary>
    public string Title { get; set; } = "Recent Activity";

    /// <summary>
    /// Gets or sets the list of activity items to display.
    /// </summary>
    public List<ActivityFeedItemViewModel> Items { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to show the refresh button in the header.
    /// </summary>
    public bool ShowRefreshButton { get; set; } = true;

    /// <summary>
    /// Gets or sets the URL for the "View all activity" link in the footer.
    /// </summary>
    public string? ViewAllUrl { get; set; }

    /// <summary>
    /// Gets or sets the maximum height of the scrollable timeline area.
    /// </summary>
    public string MaxHeight { get; set; } = "400px";

    /// <summary>
    /// Gets whether the feed has any items to display.
    /// </summary>
    public bool HasItems => Items.Any();
}
