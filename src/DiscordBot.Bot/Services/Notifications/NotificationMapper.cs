using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Services.Notifications;

/// <summary>
/// Static helper for mapping notification entities to DTOs.
/// </summary>
public static class NotificationMapper
{
    /// <summary>
    /// Maps a <see cref="UserNotification"/> entity to a <see cref="UserNotificationDto"/>.
    /// </summary>
    public static UserNotificationDto ToDto(UserNotification notification)
    {
        return new UserNotificationDto
        {
            Id = notification.Id,
            Type = notification.Type,
            TypeDisplay = GetTypeDisplayName(notification.Type),
            Severity = notification.Severity,
            Title = notification.Title,
            Message = notification.Message,
            LinkUrl = notification.LinkUrl,
            GuildId = notification.GuildId,
            GuildName = notification.Guild?.Name,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt,
            RelatedEntityType = notification.RelatedEntityType,
            RelatedEntityId = notification.RelatedEntityId,
            TimeAgo = GetTimeAgo(notification.CreatedAt)
        };
    }

    /// <summary>
    /// Gets the display name for a notification type.
    /// </summary>
    public static string GetTypeDisplayName(NotificationType type)
    {
        return type switch
        {
            NotificationType.PerformanceAlert => "Performance Alert",
            NotificationType.BotStatus => "Bot Status",
            NotificationType.GuildEvent => "Guild Event",
            NotificationType.CommandError => "Command Error",
            _ => type.ToString()
        };
    }

    /// <summary>
    /// Gets a human-readable relative time string.
    /// </summary>
    public static string GetTimeAgo(DateTime createdAt)
    {
        var diff = DateTime.UtcNow - createdAt;

        if (diff.TotalMinutes < 1)
            return "just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes} minute{((int)diff.TotalMinutes == 1 ? "" : "s")} ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours} hour{((int)diff.TotalHours == 1 ? "" : "s")} ago";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays} day{((int)diff.TotalDays == 1 ? "" : "s")} ago";
        if (diff.TotalDays < 30)
            return $"{(int)(diff.TotalDays / 7)} week{((int)(diff.TotalDays / 7) == 1 ? "" : "s")} ago";

        return createdAt.ToString("MMM d, yyyy");
    }
}
