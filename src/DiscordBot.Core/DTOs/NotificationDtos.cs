using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for reading user notification details.
/// </summary>
public record UserNotificationDto
{
    /// <summary>
    /// Unique identifier for the notification.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The type of notification.
    /// </summary>
    public NotificationType Type { get; init; }

    /// <summary>
    /// Human-readable display name for the notification type.
    /// </summary>
    public string TypeDisplay { get; init; } = string.Empty;

    /// <summary>
    /// Severity level for PerformanceAlert notifications.
    /// Null for other notification types.
    /// </summary>
    public AlertSeverity? Severity { get; init; }

    /// <summary>
    /// Short title displayed in the notification list.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Detailed message content for the notification.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional URL to navigate to when the notification is clicked.
    /// </summary>
    public string? LinkUrl { get; init; }

    /// <summary>
    /// Guild ID if this is a guild-specific notification.
    /// </summary>
    public ulong? GuildId { get; init; }

    /// <summary>
    /// Guild name for display purposes.
    /// Null if not a guild-specific notification or guild not found.
    /// </summary>
    public string? GuildName { get; init; }

    /// <summary>
    /// Whether the notification has been read.
    /// </summary>
    public bool IsRead { get; init; }

    /// <summary>
    /// Timestamp when the notification was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the notification was marked as read (UTC).
    /// </summary>
    public DateTime? ReadAt { get; init; }

    /// <summary>
    /// Type name of the related entity.
    /// </summary>
    public string? RelatedEntityType { get; init; }

    /// <summary>
    /// ID of the related entity.
    /// </summary>
    public string? RelatedEntityId { get; init; }

    /// <summary>
    /// Human-readable relative time since creation (e.g., "5 minutes ago").
    /// </summary>
    public string TimeAgo { get; init; } = string.Empty;
}

/// <summary>
/// Data transfer object for creating a new notification.
/// </summary>
public record CreateNotificationDto
{
    /// <summary>
    /// The ApplicationUser ID to receive the notification.
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// The type of notification.
    /// </summary>
    public NotificationType Type { get; init; }

    /// <summary>
    /// Severity level for PerformanceAlert notifications.
    /// </summary>
    public AlertSeverity? Severity { get; init; }

    /// <summary>
    /// Short title for the notification.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Detailed message content.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional URL to navigate to when clicked.
    /// </summary>
    public string? LinkUrl { get; init; }

    /// <summary>
    /// Optional guild context.
    /// </summary>
    public ulong? GuildId { get; init; }

    /// <summary>
    /// Type name of the related entity.
    /// </summary>
    public string? RelatedEntityType { get; init; }

    /// <summary>
    /// ID of the related entity.
    /// </summary>
    public string? RelatedEntityId { get; init; }
}

/// <summary>
/// Data transfer object for notification summary/badge count.
/// Provides aggregate counts for the notification bell indicator.
/// </summary>
public record NotificationSummaryDto
{
    /// <summary>
    /// Total number of unread notifications.
    /// </summary>
    public int TotalUnread { get; init; }

    /// <summary>
    /// Number of unread PerformanceAlert notifications.
    /// </summary>
    public int PerformanceAlertCount { get; init; }

    /// <summary>
    /// Number of unread BotStatus notifications.
    /// </summary>
    public int BotStatusCount { get; init; }

    /// <summary>
    /// Number of unread GuildEvent notifications.
    /// </summary>
    public int GuildEventCount { get; init; }

    /// <summary>
    /// Number of unread CommandError notifications.
    /// </summary>
    public int CommandErrorCount { get; init; }

    /// <summary>
    /// Number of unread critical severity notifications.
    /// </summary>
    public int CriticalCount { get; init; }

    /// <summary>
    /// Number of unread warning severity notifications.
    /// </summary>
    public int WarningCount { get; init; }

    /// <summary>
    /// Whether there are any unread notifications.
    /// </summary>
    public bool HasUnread => TotalUnread > 0;

    /// <summary>
    /// Whether there are any critical unread notifications.
    /// </summary>
    public bool HasCritical => CriticalCount > 0;
}
