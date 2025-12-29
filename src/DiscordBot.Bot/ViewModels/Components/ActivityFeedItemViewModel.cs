using DiscordBot.Bot.ViewModels.Components.Enums;

namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// Represents a single activity feed item in the timeline.
/// </summary>
public class ActivityFeedItemViewModel
{
    /// <summary>
    /// Gets or sets the type of activity for visual styling.
    /// </summary>
    public ActivityItemType Type { get; set; }

    /// <summary>
    /// Gets or sets the main message text describing the activity.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional command text to be highlighted (e.g., "/help").
    /// </summary>
    public string? CommandText { get; set; }

    /// <summary>
    /// Gets or sets the source of the activity (e.g., "Gaming Community", "User Management").
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the activity occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets the relative time string (e.g., "2 min ago", "1 hour ago").
    /// Computed from the Timestamp property.
    /// </summary>
    public string RelativeTime
    {
        get
        {
            var timeSpan = DateTime.UtcNow - Timestamp;

            if (timeSpan.TotalMinutes < 1)
                return "just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} min ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hour{((int)timeSpan.TotalHours == 1 ? "" : "s")} ago";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} day{((int)timeSpan.TotalDays == 1 ? "" : "s")} ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)(timeSpan.TotalDays / 7)} week{((int)(timeSpan.TotalDays / 7) == 1 ? "" : "s")} ago";

            return Timestamp.ToString("MMM d, yyyy");
        }
    }

    /// <summary>
    /// Gets the CSS class name for the activity type.
    /// </summary>
    public string TypeClassName => Type.ToString().ToLowerInvariant();
}
