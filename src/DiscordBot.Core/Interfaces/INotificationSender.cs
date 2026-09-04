using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for creating (sending) user notifications.
/// </summary>
public interface INotificationSender
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
}
