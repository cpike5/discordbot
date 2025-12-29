namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for the Connected Servers widget displaying a summary of Discord servers.
/// </summary>
public record ConnectedServersWidgetViewModel
{
    /// <summary>
    /// Gets the widget title.
    /// </summary>
    public string Title { get; init; } = "Connected Servers";

    /// <summary>
    /// Gets the URL to the full servers list page.
    /// </summary>
    public string ViewAllUrl { get; init; } = "/Servers";

    /// <summary>
    /// Gets the list of servers to display in the widget.
    /// Typically shows top 5 servers by activity.
    /// </summary>
    public List<ConnectedServerItemViewModel> Servers { get; init; } = new();

    /// <summary>
    /// Gets the total number of servers the bot is connected to.
    /// </summary>
    public int TotalServerCount { get; init; }

    /// <summary>
    /// Gets whether to show the "View All" link.
    /// Shows when there are more servers than displayed in the widget.
    /// </summary>
    public bool ShowViewAll => TotalServerCount > Servers.Count;
}
