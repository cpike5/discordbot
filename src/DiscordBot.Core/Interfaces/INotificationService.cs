using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing user notifications.
/// Provides methods for creating, retrieving, and managing notification lifecycle.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Creates a notification for a specific user.
    /// </summary>
    /// <param name="userId">The ApplicationUser ID.</param>
    /// <param name="type">The notification type.</param>
    /// <param name="title">Short title for the notification.</param>
    /// <param name="message">Detailed message content.</param>
    /// <param name="linkUrl">Optional URL to navigate to when clicked.</param>
    /// <param name="severity">Optional severity for PerformanceAlert notifications.</param>
    /// <param name="guildId">Optional guild context.</param>
    /// <param name="relatedEntityType">Optional related entity type name.</param>
    /// <param name="relatedEntityId">Optional related entity ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateForUserAsync(
        string userId,
        NotificationType type,
        string title,
        string message,
        string? linkUrl = null,
        AlertSeverity? severity = null,
        ulong? guildId = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a notification for all admin users (SuperAdmin and Admin roles).
    /// </summary>
    /// <param name="type">The notification type.</param>
    /// <param name="title">Short title for the notification.</param>
    /// <param name="message">Detailed message content.</param>
    /// <param name="linkUrl">Optional URL to navigate to when clicked.</param>
    /// <param name="severity">Optional severity for PerformanceAlert notifications.</param>
    /// <param name="relatedEntityType">Optional related entity type name.</param>
    /// <param name="relatedEntityId">Optional related entity ID.</param>
    /// <param name="deduplicationWindow">Optional time window for duplicate suppression. If a matching notification exists within this window, no new notification is created.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if notifications were created; false if suppressed as duplicate.</returns>
    Task<bool> CreateForAllAdminsAsync(
        NotificationType type,
        string title,
        string message,
        string? linkUrl = null,
        AlertSeverity? severity = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        TimeSpan? deduplicationWindow = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a notification for all users with admin access to a specific guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="type">The notification type.</param>
    /// <param name="title">Short title for the notification.</param>
    /// <param name="message">Detailed message content.</param>
    /// <param name="linkUrl">Optional URL to navigate to when clicked.</param>
    /// <param name="relatedEntityType">Optional related entity type name.</param>
    /// <param name="relatedEntityId">Optional related entity ID.</param>
    /// <param name="deduplicationWindow">Optional time window for duplicate suppression. If a matching notification exists within this window, no new notification is created.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if notifications were created; false if suppressed as duplicate.</returns>
    Task<bool> CreateForGuildAdminsAsync(
        ulong guildId,
        NotificationType type,
        string title,
        string message,
        string? linkUrl = null,
        string? relatedEntityType = null,
        string? relatedEntityId = null,
        TimeSpan? deduplicationWindow = null,
        CancellationToken cancellationToken = default);

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
}
