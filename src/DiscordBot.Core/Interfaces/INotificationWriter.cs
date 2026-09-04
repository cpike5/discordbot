using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Write-side repository interface for mutating and purging user notifications.
/// </summary>
public interface INotificationWriter
{
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
    /// Permanently deletes all notifications for a user.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of notifications deleted.</returns>
    Task<int> DeleteAllByUserAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
