namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// ViewModel for the live activity feed component.
/// </summary>
/// <param name="IsPaused">Whether the activity feed is currently paused (defaults to false).</param>
/// <param name="MaxItems">Maximum number of activity items to display (defaults to 15).</param>
/// <param name="EmptyMessage">Message to display when there are no activities (defaults to "No recent activity").</param>
public record ActivityFeedViewModel(
    bool IsPaused = false,
    int MaxItems = 15,
    string EmptyMessage = "No recent activity"
);
