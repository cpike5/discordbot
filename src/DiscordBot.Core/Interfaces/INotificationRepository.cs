using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for UserNotification entities with notification-specific operations.
/// </summary>
public interface INotificationRepository : IRepository<UserNotification>
{
    /// <summary>
    /// Gets notifications for a specific user with pagination and optional filtering.
    /// </summary>
    /// <param name="userId">ApplicationUser ID to filter by.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="type">Optional notification type filter.</param>
    /// <param name="severity">Optional severity filter.</param>
    /// <param name="isRead">Optional read status filter (true for read only, false for unread only).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated notifications and the total count.</returns>
    Task<(IEnumerable<UserNotification> Items, int TotalCount)> GetByUserAsync(
        string userId,
        int page,
        int pageSize,
        NotificationType? type = null,
        AlertSeverity? severity = null,
        bool? isRead = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of unread notifications for a specific user.
    /// </summary>
    /// <param name="userId">ApplicationUser ID to count for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of unread notifications.</returns>
    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets notification summary statistics for a specific user.
    /// Returns counts by severity for unread notifications.
    /// </summary>
    /// <param name="userId">ApplicationUser ID to get summary for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing unread, critical, warning, and info counts.</returns>
    Task<(int UnreadCount, int CriticalCount, int WarningCount, int InfoCount)> GetSummaryAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent unread notifications for a specific user.
    /// Used for quick display in notification dropdown.
    /// </summary>
    /// <param name="userId">ApplicationUser ID to get notifications for.</param>
    /// <param name="limit">Maximum number of notifications to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of recent unread notifications ordered by creation time descending.</returns>
    Task<IEnumerable<UserNotification>> GetRecentUnreadAsync(
        string userId,
        int limit = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a notification by ID for a specific user.
    /// Used to ensure users can only access their own notifications.
    /// </summary>
    /// <param name="id">Notification ID.</param>
    /// <param name="userId">ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The notification if found and owned by the user, otherwise null.</returns>
    Task<UserNotification?> GetByIdForUserAsync(
        Guid id,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    /// <param name="id">Notification ID.</param>
    /// <param name="userId">ApplicationUser ID (for ownership verification).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the notification was found and marked as read, false otherwise.</returns>
    Task<bool> MarkAsReadAsync(
        Guid id,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all unread notifications as read for a specific user.
    /// </summary>
    /// <param name="userId">ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of notifications marked as read.</returns>
    Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old notifications older than the specified date.
    /// Used for retention policy cleanup.
    /// </summary>
    /// <param name="olderThan">Delete notifications created before this UTC date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of notifications deleted.</returns>
    Task<int> DeleteOldNotificationsAsync(DateTime olderThan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates notifications for multiple users in a single batch operation.
    /// Useful for broadcasting alerts to all administrators.
    /// </summary>
    /// <param name="notifications">Collection of notifications to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of notifications created.</returns>
    Task<int> CreateBatchAsync(
        IEnumerable<UserNotification> notifications,
        CancellationToken cancellationToken = default);
}
