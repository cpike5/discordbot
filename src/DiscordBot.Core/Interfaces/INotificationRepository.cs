using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for managing user notifications.
/// Provides methods for retrieving, creating, and managing notification lifecycle.
/// </summary>
public interface INotificationRepository : IRepository<UserNotification>
{
    /// <summary>
    /// Retrieves notifications for a specific user.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID.</param>
    /// <param name="limit">Maximum number of notifications to return (default: 15).</param>
    /// <param name="includeRead">Whether to include read notifications (default: true).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of notifications ordered by creation date descending.</returns>
    Task<IEnumerable<UserNotification>> GetUserNotificationsAsync(
        string userId,
        int limit = 15,
        bool includeRead = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a summary of unread notification counts for a user.
    /// Used for displaying the notification badge count.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary with counts by type and severity.</returns>
    Task<NotificationSummaryDto> GetUserNotificationSummaryAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a notification by its unique identifier.
    /// </summary>
    /// <param name="id">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The notification if found; otherwise, null.</returns>
    Task<UserNotification?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read.
    /// Sets IsRead to true and ReadAt to current UTC time.
    /// </summary>
    /// <param name="id">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkAsReadAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all notifications for a user as read.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dismisses a notification (soft delete).
    /// Sets DismissedAt to current UTC time.
    /// </summary>
    /// <param name="id">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DismissAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes dismissed notifications older than the cutoff date in batches.
    /// </summary>
    /// <param name="cutoff">The cutoff date; notifications dismissed before this date will be deleted.</param>
    /// <param name="batchSize">Maximum number of records to delete in this batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of notifications deleted in this batch.</returns>
    Task<int> DeleteDismissedOlderThanAsync(
        DateTime cutoff,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes read (but not dismissed) notifications older than the cutoff date in batches.
    /// </summary>
    /// <param name="cutoff">The cutoff date; read notifications created before this date will be deleted.</param>
    /// <param name="batchSize">Maximum number of records to delete in this batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of notifications deleted in this batch.</returns>
    Task<int> DeleteReadOlderThanAsync(
        DateTime cutoff,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes unread (and not dismissed) notifications older than the cutoff date in batches.
    /// </summary>
    /// <param name="cutoff">The cutoff date; unread notifications created before this date will be deleted.</param>
    /// <param name="batchSize">Maximum number of records to delete in this batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of notifications deleted in this batch.</returns>
    Task<int> DeleteUnreadOlderThanAsync(
        DateTime cutoff,
        int batchSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple notifications in a single database operation.
    /// More efficient than calling AddAsync multiple times.
    /// </summary>
    /// <param name="notifications">The notifications to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddRangeAsync(
        IEnumerable<UserNotification> notifications,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a recent notification with the same type and related entity exists.
    /// Used for duplicate suppression to avoid spamming notifications.
    /// </summary>
    /// <param name="type">The notification type.</param>
    /// <param name="relatedEntityType">The related entity type name.</param>
    /// <param name="relatedEntityId">The related entity ID.</param>
    /// <param name="window">The time window to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a matching notification exists within the window; otherwise, false.</returns>
    Task<bool> HasRecentNotificationAsync(
        NotificationType type,
        string? relatedEntityType,
        string? relatedEntityId,
        TimeSpan window,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves paginated notifications for a user with filtering.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID.</param>
    /// <param name="query">Query parameters including filters and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of notification list and total count.</returns>
    Task<(IReadOnlyList<UserNotification> Items, int TotalCount)> GetUserNotificationsPagedAsync(
        string userId,
        NotificationQueryDto query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks multiple notifications as read.
    /// </summary>
    /// <param name="ids">The notification IDs to mark as read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkMultipleAsReadAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as unread.
    /// </summary>
    /// <param name="id">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkAsUnreadAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a notification.
    /// </summary>
    /// <param name="id">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes multiple notifications.
    /// </summary>
    /// <param name="ids">The notification IDs to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of notifications deleted.</returns>
    Task<int> DeleteMultipleAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the IDs of notifications owned by a specific user from a given set of IDs.
    /// Used for validating ownership before bulk operations.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID.</param>
    /// <param name="notificationIds">The notification IDs to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of notification IDs that belong to the user.</returns>
    Task<IReadOnlyList<Guid>> GetOwnedNotificationIdsAsync(
        string userId,
        IEnumerable<Guid> notificationIds,
        CancellationToken cancellationToken = default);
}
