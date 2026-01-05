using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing user notifications in the admin UI.
/// Provides high-level operations for creating, retrieving, and managing notifications.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Creates a new notification for a user.
    /// </summary>
    /// <param name="dto">The notification creation data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created notification.</returns>
    Task<UserNotification> CreateNotificationAsync(
        CreateNotificationDto dto,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates notifications for multiple users (broadcast).
    /// Useful for alerting all administrators about critical events.
    /// </summary>
    /// <param name="userIds">Collection of ApplicationUser IDs to notify.</param>
    /// <param name="type">Notification type.</param>
    /// <param name="title">Notification title.</param>
    /// <param name="message">Notification message.</param>
    /// <param name="severity">Notification severity.</param>
    /// <param name="actionUrl">Optional action URL.</param>
    /// <param name="relatedEntityId">Optional related entity ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of notifications created.</returns>
    Task<int> CreateBroadcastNotificationAsync(
        IEnumerable<string> userIds,
        NotificationType type,
        string title,
        string message,
        AlertSeverity severity,
        string? actionUrl = null,
        string? relatedEntityId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets notifications for a user with pagination and optional filtering.
    /// </summary>
    /// <param name="userId">ApplicationUser ID.</param>
    /// <param name="query">Query parameters for filtering and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated notification results.</returns>
    Task<NotificationPagedResultDto> GetNotificationsAsync(
        string userId,
        NotificationQueryDto query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a notification by ID for a specific user.
    /// </summary>
    /// <param name="id">Notification ID.</param>
    /// <param name="userId">ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The notification DTO if found, otherwise null.</returns>
    Task<UserNotificationDto?> GetNotificationAsync(
        Guid id,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the notification summary for a user (counts and recent notifications).
    /// Used for displaying notification badge and dropdown.
    /// </summary>
    /// <param name="userId">ApplicationUser ID.</param>
    /// <param name="recentLimit">Maximum number of recent notifications to include.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Notification summary with counts and recent notifications.</returns>
    Task<NotificationSummaryDto> GetSummaryAsync(
        string userId,
        int recentLimit = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of unread notifications for a user.
    /// </summary>
    /// <param name="userId">ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The unread notification count.</returns>
    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    /// <param name="id">Notification ID.</param>
    /// <param name="userId">ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the notification was marked as read, false if not found.</returns>
    Task<bool> MarkAsReadAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all unread notifications as read for a user.
    /// </summary>
    /// <param name="userId">ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of notifications marked as read.</returns>
    Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a notification.
    /// </summary>
    /// <param name="id">Notification ID.</param>
    /// <param name="userId">ApplicationUser ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the notification was deleted, false if not found.</returns>
    Task<bool> DeleteNotificationAsync(Guid id, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes old notifications based on retention policy.
    /// </summary>
    /// <param name="retentionDays">Number of days to retain notifications.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of notifications deleted.</returns>
    Task<int> CleanupOldNotificationsAsync(int retentionDays, CancellationToken cancellationToken = default);
}
