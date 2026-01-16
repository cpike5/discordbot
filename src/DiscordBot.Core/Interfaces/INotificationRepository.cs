using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

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
    /// Deletes notifications that were dismissed more than the specified number of days ago.
    /// Used for retention cleanup.
    /// </summary>
    /// <param name="daysToKeep">Number of days to keep dismissed notifications.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of notifications deleted.</returns>
    Task<int> CleanupOldNotificationsAsync(
        int daysToKeep,
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
}
