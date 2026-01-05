using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for reading user notification details.
/// </summary>
public record UserNotificationDto
{
    /// <summary>
    /// Unique identifier for this notification.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The type of notification.
    /// </summary>
    public NotificationType Type { get; init; }

    /// <summary>
    /// Short title/subject of the notification.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Detailed message content of the notification.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Severity level of the notification.
    /// </summary>
    public AlertSeverity Severity { get; init; }

    /// <summary>
    /// Optional URL or route the notification links to for more details.
    /// </summary>
    public string? ActionUrl { get; init; }

    /// <summary>
    /// Optional related entity ID for context.
    /// </summary>
    public string? RelatedEntityId { get; init; }

    /// <summary>
    /// UTC timestamp when this notification was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// UTC timestamp when the user read/acknowledged this notification.
    /// Null if unread.
    /// </summary>
    public DateTime? ReadAt { get; init; }

    /// <summary>
    /// Whether this notification has been read by the user.
    /// </summary>
    public bool IsRead { get; init; }

    /// <summary>
    /// Human-readable relative time since creation (e.g., "5 minutes ago").
    /// Populated at mapping time.
    /// </summary>
    public string? TimeAgo { get; init; }
}

/// <summary>
/// Data transfer object for creating a new notification.
/// </summary>
public record CreateNotificationDto
{
    /// <summary>
    /// The ID of the ApplicationUser who should receive this notification.
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// The type of notification.
    /// </summary>
    public NotificationType Type { get; init; }

    /// <summary>
    /// Short title/subject of the notification.
    /// Maximum 200 characters.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Detailed message content of the notification.
    /// Maximum 1000 characters.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Severity level of the notification.
    /// </summary>
    public AlertSeverity Severity { get; init; }

    /// <summary>
    /// Optional URL or route the notification links to for more details.
    /// Maximum 500 characters.
    /// </summary>
    public string? ActionUrl { get; init; }

    /// <summary>
    /// Optional related entity ID for context.
    /// Maximum 100 characters.
    /// </summary>
    public string? RelatedEntityId { get; init; }
}

/// <summary>
/// Data transfer object for notification summary/count information.
/// Used for badge counts and quick status display.
/// </summary>
public record NotificationSummaryDto
{
    /// <summary>
    /// Total number of unread notifications for the user.
    /// </summary>
    public int UnreadCount { get; init; }

    /// <summary>
    /// Number of unread critical notifications.
    /// </summary>
    public int CriticalCount { get; init; }

    /// <summary>
    /// Number of unread warning notifications.
    /// </summary>
    public int WarningCount { get; init; }

    /// <summary>
    /// Number of unread info notifications.
    /// </summary>
    public int InfoCount { get; init; }

    /// <summary>
    /// The most recent unread notifications (limited to a small number for quick display).
    /// </summary>
    public IReadOnlyList<UserNotificationDto> RecentNotifications { get; init; } = Array.Empty<UserNotificationDto>();
}

/// <summary>
/// Data transfer object for querying notifications with pagination and filtering.
/// </summary>
public record NotificationQueryDto
{
    /// <summary>
    /// Page number (1-based).
    /// </summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; } = 20;

    /// <summary>
    /// Filter by notification type.
    /// Null to include all types.
    /// </summary>
    public NotificationType? Type { get; init; }

    /// <summary>
    /// Filter by severity level.
    /// Null to include all severities.
    /// </summary>
    public AlertSeverity? Severity { get; init; }

    /// <summary>
    /// Filter by read status.
    /// True for read only, false for unread only, null for all.
    /// </summary>
    public bool? IsRead { get; init; }

    /// <summary>
    /// Filter notifications created on or after this date (UTC).
    /// Null to not filter by start date.
    /// </summary>
    public DateTime? StartDate { get; init; }

    /// <summary>
    /// Filter notifications created before this date (UTC).
    /// Null to not filter by end date.
    /// </summary>
    public DateTime? EndDate { get; init; }
}

/// <summary>
/// Data transfer object for paginated notification results.
/// </summary>
public record NotificationPagedResultDto
{
    /// <summary>
    /// Collection of notifications for the current page.
    /// </summary>
    public IReadOnlyList<UserNotificationDto> Items { get; init; } = Array.Empty<UserNotificationDto>();

    /// <summary>
    /// Total number of notifications matching the query (across all pages).
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    public int PageNumber { get; init; }

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Total number of pages available.
    /// </summary>
    public int TotalPages { get; init; }
}
