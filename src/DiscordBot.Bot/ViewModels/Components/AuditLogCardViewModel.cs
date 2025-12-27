using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for displaying recent audit log entries on the dashboard.
/// </summary>
public record AuditLogCardViewModel
{
    /// <summary>
    /// Gets the collection of recent audit log items.
    /// </summary>
    public IReadOnlyList<AuditLogItem> Logs { get; init; } = Array.Empty<AuditLogItem>();

    /// <summary>
    /// Creates an <see cref="AuditLogCardViewModel"/> from a collection of audit log DTOs.
    /// </summary>
    /// <param name="logs">The audit log DTOs to convert into display items.</param>
    /// <returns>A new <see cref="AuditLogCardViewModel"/> instance with the audit log items.</returns>
    public static AuditLogCardViewModel FromLogs(IEnumerable<AuditLogDto> logs)
    {
        var items = logs
            .Select(log => new AuditLogItem(
                Id: log.Id,
                Timestamp: log.Timestamp,
                RelativeTime: FormatRelativeTime(log.Timestamp),
                Category: log.Category,
                CategoryName: log.CategoryName,
                CategoryIcon: GetCategoryIcon(log.Category),
                Action: log.Action,
                ActionName: log.ActionName,
                ActorDisplayName: log.ActorDisplayName ?? "System",
                TargetType: log.TargetType,
                TargetId: log.TargetId,
                GuildName: log.GuildName,
                Description: FormatDescription(log)
            ))
            .ToList();

        return new AuditLogCardViewModel
        {
            Logs = items
        };
    }

    /// <summary>
    /// Formats a timestamp into a human-readable relative time string.
    /// </summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <returns>A relative time string (e.g., "Just now", "5 min ago", "2 hours ago", "Dec 5").</returns>
    private static string FormatRelativeTime(DateTime timestamp)
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

    /// <summary>
    /// Gets the SVG path for the icon representing the audit log category.
    /// </summary>
    /// <param name="category">The audit log category.</param>
    /// <returns>The SVG path string for the category icon.</returns>
    private static string GetCategoryIcon(AuditLogCategory category)
    {
        return category switch
        {
            AuditLogCategory.User => "M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z",
            AuditLogCategory.Guild => "M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4",
            AuditLogCategory.Configuration => "M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z",
            AuditLogCategory.Security => "M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z",
            AuditLogCategory.Command => "M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z",
            AuditLogCategory.Message => "M8 10h.01M12 10h.01M16 10h.01M9 16H5a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v8a2 2 0 01-2 2h-5l-5 5v-5z",
            AuditLogCategory.System => "M9 3v2m6-2v2M9 19v2m6-2v2M5 9H3m2 6H3m18-6h-2m2 6h-2M7 19h10a2 2 0 002-2V7a2 2 0 00-2-2H7a2 2 0 00-2 2v10a2 2 0 002 2zM9 9h6v6H9V9z",
            _ => "M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
        };
    }

    /// <summary>
    /// Formats a brief description for the audit log entry.
    /// </summary>
    /// <param name="log">The audit log DTO.</param>
    /// <returns>A brief description string.</returns>
    private static string FormatDescription(AuditLogDto log)
    {
        var actor = log.ActorDisplayName ?? "System";
        var target = log.TargetId != null ? $"{log.TargetType}" : "";

        return log.Action switch
        {
            AuditLogAction.Created => $"{actor} created {target}",
            AuditLogAction.Updated => $"{actor} updated {target}",
            AuditLogAction.Deleted => $"{actor} deleted {target}",
            AuditLogAction.Login => $"{actor} logged in",
            AuditLogAction.Logout => $"{actor} logged out",
            AuditLogAction.PermissionChanged => $"{actor} changed permissions",
            AuditLogAction.SettingChanged => $"{actor} changed settings",
            AuditLogAction.CommandExecuted => $"{actor} executed command",
            AuditLogAction.MessageDeleted => $"{actor} deleted message",
            AuditLogAction.MessageEdited => $"{actor} edited message",
            AuditLogAction.UserBanned => $"{actor} banned user",
            AuditLogAction.UserUnbanned => $"{actor} unbanned user",
            AuditLogAction.UserKicked => $"{actor} kicked user",
            AuditLogAction.RoleAssigned => $"{actor} assigned role",
            AuditLogAction.RoleRemoved => $"{actor} removed role",
            _ => $"{actor} performed {log.ActionName}"
        };
    }
}

/// <summary>
/// Represents a single audit log item in the dashboard card.
/// </summary>
/// <param name="Id">The unique identifier for the audit log entry.</param>
/// <param name="Timestamp">The timestamp when the action occurred (UTC).</param>
/// <param name="RelativeTime">The human-readable relative time (e.g., "5 min ago").</param>
/// <param name="Category">The category of the audit log entry.</param>
/// <param name="CategoryName">The category name as a string.</param>
/// <param name="CategoryIcon">The SVG path for the category icon.</param>
/// <param name="Action">The specific action that was performed.</param>
/// <param name="ActionName">The action name as a string.</param>
/// <param name="ActorDisplayName">The display name of the actor who performed the action.</param>
/// <param name="TargetType">The type of entity that was affected.</param>
/// <param name="TargetId">The identifier of the entity that was affected.</param>
/// <param name="GuildName">The guild name for display purposes.</param>
/// <param name="Description">A brief description of the audit log entry.</param>
public record AuditLogItem(
    long Id,
    DateTime Timestamp,
    string RelativeTime,
    AuditLogCategory Category,
    string CategoryName,
    string CategoryIcon,
    AuditLogAction Action,
    string ActionName,
    string ActorDisplayName,
    string? TargetType,
    string? TargetId,
    string? GuildName,
    string Description
);
