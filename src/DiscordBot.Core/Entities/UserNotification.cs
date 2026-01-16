using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a notification for an admin UI user.
/// Notifications are displayed in the notification bell dropdown and can be
/// marked as read, dismissed, or automatically cleaned up after retention period.
/// </summary>
public class UserNotification
{
    /// <summary>
    /// Unique identifier for the notification.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The ApplicationUser ID (ASP.NET Core Identity user) who receives this notification.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The type of notification (PerformanceAlert, BotStatus, GuildEvent, CommandError).
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Optional severity level for PerformanceAlert notifications.
    /// Null for other notification types.
    /// </summary>
    public AlertSeverity? Severity { get; set; }

    /// <summary>
    /// Short title displayed in the notification list.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed message content for the notification.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional URL to navigate to when the notification is clicked.
    /// </summary>
    public string? LinkUrl { get; set; }

    /// <summary>
    /// Optional guild context for guild-specific notifications.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Whether the notification has been read by the user.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Timestamp when the notification was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the notification was marked as read (UTC).
    /// Null if not yet read.
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Timestamp when the notification was dismissed (UTC).
    /// Dismissed notifications are soft-deleted and cleaned up after retention period.
    /// </summary>
    public DateTime? DismissedAt { get; set; }

    /// <summary>
    /// Type name of the related entity (e.g., "PerformanceIncident", "GuildSettings").
    /// Used for linking to source entities.
    /// </summary>
    public string? RelatedEntityType { get; set; }

    /// <summary>
    /// ID of the related entity.
    /// </summary>
    public string? RelatedEntityId { get; set; }

    /// <summary>
    /// Navigation property to the ApplicationUser who receives this notification.
    /// </summary>
    public ApplicationUser User { get; set; } = null!;

    /// <summary>
    /// Navigation property to the Guild if this is a guild-specific notification.
    /// </summary>
    public Guild? Guild { get; set; }
}
