using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Provides per-user notification query/mutation operations for the dashboard hub.
/// Extracted from <see cref="DiscordBot.Bot.Hubs.DashboardHub"/> to keep the hub thin.
/// </summary>
public interface IDashboardNotificationQueryService
{
    /// <summary>
    /// Gets the notification summary (unread count by type) for the given user.
    /// </summary>
    Task<NotificationSummaryDto> GetNotificationSummaryAsync(string? userId, string? connectionId);

    /// <summary>
    /// Gets recent notifications for the given user.
    /// </summary>
    Task<IEnumerable<UserNotificationDto>> GetNotificationsAsync(string? userId, string? connectionId, int limit);

    /// <summary>
    /// Marks a notification as read for the given user.
    /// </summary>
    Task MarkNotificationReadAsync(string? userId, string? connectionId, Guid notificationId);

    /// <summary>
    /// Marks all notifications as read for the given user.
    /// </summary>
    Task MarkAllNotificationsReadAsync(string? userId, string? connectionId);

    /// <summary>
    /// Dismisses (soft deletes) a notification for the given user.
    /// </summary>
    Task DismissNotificationAsync(string? userId, string? connectionId, Guid notificationId);
}
