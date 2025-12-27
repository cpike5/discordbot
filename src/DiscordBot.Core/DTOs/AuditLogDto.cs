using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object representing an audit log entry for display purposes.
/// Includes parsed details and user-friendly information.
/// </summary>
public class AuditLogDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this audit log entry.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the action occurred (UTC).
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the category of the audit log entry.
    /// </summary>
    public AuditLogCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the category name as a string.
    /// </summary>
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the specific action that was performed.
    /// </summary>
    public AuditLogAction Action { get; set; }

    /// <summary>
    /// Gets or sets the action name as a string.
    /// </summary>
    public string ActionName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the actor who performed the action.
    /// </summary>
    public string? ActorId { get; set; }

    /// <summary>
    /// Gets or sets the type of actor that performed the action.
    /// </summary>
    public AuditLogActorType ActorType { get; set; }

    /// <summary>
    /// Gets or sets the actor type name as a string.
    /// </summary>
    public string ActorTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the actor (username, system name, etc.).
    /// </summary>
    public string? ActorDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the type name of the entity that was affected.
    /// </summary>
    public string? TargetType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the entity that was affected.
    /// </summary>
    public string? TargetId { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild ID associated with this action.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the guild name for display purposes.
    /// </summary>
    public string? GuildName { get; set; }

    /// <summary>
    /// Gets or sets the additional contextual information as a JSON string.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets the IP address from which the action was performed.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID to group related audit log entries.
    /// </summary>
    public string? CorrelationId { get; set; }
}
