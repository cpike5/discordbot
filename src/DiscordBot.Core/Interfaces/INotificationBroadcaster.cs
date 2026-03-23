using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Broadcasts notification events to connected clients via SignalR.
/// Decouples real-time push from the notification CRUD service.
/// </summary>
public interface INotificationBroadcaster
{
    /// <summary>
    /// Broadcasts a new notification and updated summary to a specific user.
    /// </summary>
    /// <param name="userId">The user ID to broadcast to.</param>
    /// <param name="notification">The notification entity to broadcast.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastNotificationAsync(
        string userId,
        UserNotification notification,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts the notification-marked-read event and updated count to a specific user.
    /// </summary>
    /// <param name="userId">The user ID to broadcast to.</param>
    /// <param name="notificationId">The notification ID that was marked as read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastNotificationMarkedReadAsync(
        string userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts an updated notification count to a specific user.
    /// Used when a notification is dismissed, deleted, or marked as unread.
    /// </summary>
    /// <param name="userId">The user ID to broadcast to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastCountChangedAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts the all-notifications-read event and updated count to a specific user.
    /// </summary>
    /// <param name="userId">The user ID to broadcast to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BroadcastAllReadAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
