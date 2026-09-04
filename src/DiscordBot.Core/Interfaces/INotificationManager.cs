using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for retrieving and managing the lifecycle of a user's notifications
/// (read/query, mark read/unread, dismiss, and delete).
/// </summary>
public interface INotificationManager
{
    /// <summary>
    /// Retrieves notifications for a user.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID.</param>
    /// <param name="limit">Maximum number of notifications to return (default: 15).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of notification DTOs ordered by creation date descending.</returns>
    Task<IEnumerable<UserNotificationDto>> GetUserNotificationsAsync(
        string userId,
        int limit = 15,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the notification summary for a user (badge count).
    /// </summary>
    /// <param name="userId">The ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary with unread counts by type and severity.</returns>
    Task<NotificationSummaryDto> GetSummaryAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read.
    /// Validates that the notification belongs to the specified user.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID (for ownership validation).</param>
    /// <param name="notificationId">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkAsReadAsync(
        string userId,
        Guid notificationId,
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
    /// Validates that the notification belongs to the specified user.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID (for ownership validation).</param>
    /// <param name="notificationId">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DismissAsync(
        string userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves paginated notifications for a user with filtering.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID.</param>
    /// <param name="query">Query parameters including filters and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated response with notification DTOs.</returns>
    Task<PaginatedResponseDto<UserNotificationDto>> GetUserNotificationsPagedAsync(
        string userId,
        NotificationQueryDto query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks multiple notifications as read for a user.
    /// Validates ownership of each notification.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID (for ownership validation).</param>
    /// <param name="notificationIds">The notification IDs to mark as read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task MarkMultipleAsReadAsync(
        string userId,
        IEnumerable<Guid> notificationIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as unread for a user.
    /// Validates that the notification belongs to the specified user.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID (for ownership validation).</param>
    /// <param name="notificationId">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful; false if notification not found or not owned by user.</returns>
    Task<bool> MarkAsUnreadAsync(
        string userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes a notification for a user.
    /// Validates that the notification belongs to the specified user.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID (for ownership validation).</param>
    /// <param name="notificationId">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successful; false if notification not found or not owned by user.</returns>
    Task<bool> DeleteAsync(
        string userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently deletes multiple notifications for a user.
    /// Validates ownership of each notification.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID (for ownership validation).</param>
    /// <param name="notificationIds">The notification IDs to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of notifications deleted.</returns>
    Task<int> DeleteMultipleAsync(
        string userId,
        IEnumerable<Guid> notificationIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all notifications for a user.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of notifications deleted.</returns>
    Task<int> DeleteAllAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
