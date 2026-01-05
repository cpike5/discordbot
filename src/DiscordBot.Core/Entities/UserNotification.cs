using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a notification sent to a user in the admin UI.
/// Notifications are used to inform administrators about important events
/// such as performance alerts, bot status changes, and guild events.
/// </summary>
public class UserNotification
{
    /// <summary>
    /// Unique identifier for this notification.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The ID of the ApplicationUser who should receive this notification.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The type of notification.
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Short title/subject of the notification.
    /// Maximum 200 characters.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Detailed message content of the notification.
    /// Maximum 1000 characters.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Severity level of the notification.
    /// Uses the existing AlertSeverity enum for consistency.
    /// </summary>
    public AlertSeverity Severity { get; set; }

    /// <summary>
    /// Optional URL or route the notification links to for more details.
    /// Examples: "/Admin/Performance/Alerts", "/Guilds/Details?id=123".
    /// Maximum 500 characters.
    /// </summary>
    public string? ActionUrl { get; set; }

    /// <summary>
    /// Optional related entity ID for context.
    /// Could be a guild ID, incident ID, command log ID, etc.
    /// Maximum 100 characters.
    /// </summary>
    public string? RelatedEntityId { get; set; }

    /// <summary>
    /// UTC timestamp when this notification was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp when the user read/acknowledged this notification.
    /// Null if unread.
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Whether this notification has been read by the user.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Navigation property to the ApplicationUser who receives this notification.
    /// </summary>
    public ApplicationUser? User { get; set; }
}
