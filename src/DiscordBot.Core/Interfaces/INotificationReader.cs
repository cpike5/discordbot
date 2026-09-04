using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Read-side repository interface for querying user notifications.
/// </summary>
public interface INotificationReader : IRepository<UserNotification>
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
