using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying recent command activity on the dashboard.
/// </summary>
public record RecentActivityViewModel
{
    /// <summary>
    /// Gets the collection of recent activity items.
    /// </summary>
    public IReadOnlyList<ActivityItem> Activities { get; init; } = Array.Empty<ActivityItem>();

    /// <summary>
    /// Creates a <see cref="RecentActivityViewModel"/> from a collection of command log DTOs.
    /// </summary>
    /// <param name="logs">The command log DTOs to convert into activity items.</param>
    /// <returns>A new <see cref="RecentActivityViewModel"/> instance with the activity items.</returns>
    public static RecentActivityViewModel FromLogs(IEnumerable<CommandLogDto> logs)
    {
        var activities = logs
            .Select(log => new ActivityItem(
                Id: log.Id,
                CommandName: log.CommandName,
                GuildName: log.GuildName ?? "Direct Message",
                Username: log.Username ?? $"User {log.UserId}",
                ExecutedAt: log.ExecutedAt,
                RelativeTime: FormatRelativeTime(log.ExecutedAt),
                Success: log.Success,
                ErrorMessage: log.ErrorMessage
            ))
            .ToList();

        return new RecentActivityViewModel
        {
            Activities = activities
        };
    }

    /// <summary>
    /// Formats a timestamp into a human-readable relative time string.
    /// </summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <returns>A relative time string (e.g., "Just now", "5 min ago", "2 hours ago", "Dec 5").</returns>
    public static string FormatRelativeTime(DateTime timestamp)
    {
        var now = DateTime.UtcNow;
        var difference = now - timestamp;

        if (difference.TotalMinutes < 1)
        {
            return "Just now";
        }
        else if (difference.TotalMinutes < 60)
        {
            var minutes = (int)difference.TotalMinutes;
            return $"{minutes} min ago";
        }
        else if (difference.TotalHours < 24)
        {
            var hours = (int)difference.TotalHours;
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }
        else if (difference.TotalDays < 7)
        {
            var days = (int)difference.TotalDays;
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }
        else
        {
            // Format as date for 7+ days (e.g., "Dec 5")
            return timestamp.ToString("MMM d");
        }
    }
}

/// <summary>
/// Represents a single activity item in the recent activity feed.
/// </summary>
/// <param name="Id">The unique identifier for the command log entry.</param>
/// <param name="CommandName">The name of the command that was executed.</param>
/// <param name="GuildName">The name of the guild where the command was executed.</param>
/// <param name="Username">The username of the user who executed the command.</param>
/// <param name="ExecutedAt">The timestamp when the command was executed.</param>
/// <param name="RelativeTime">The human-readable relative time (e.g., "5 min ago").</param>
/// <param name="Success">Whether the command executed successfully.</param>
/// <param name="ErrorMessage">The error message if the command failed.</param>
public record ActivityItem(
    Guid Id,
    string CommandName,
    string GuildName,
    string Username,
    DateTime ExecutedAt,
    string RelativeTime,
    bool Success,
    string? ErrorMessage
);
