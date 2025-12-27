using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents an audit log entry that tracks actions performed in the system.
/// Used for security monitoring, compliance, and user activity tracking.
/// </summary>
public class AuditLog
{
    /// <summary>
    /// Gets or sets the unique identifier for this audit log entry.
    /// Uses long (Int64) to support high-volume logging scenarios.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the action occurred.
    /// Stored in UTC.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the category of the audit log entry.
    /// Groups related actions together (e.g., User, Guild, Security).
    /// </summary>
    public AuditLogCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the specific action that was performed.
    /// </summary>
    public AuditLogAction Action { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the actor who performed the action.
    /// Can be a user ID, system identifier, or null for anonymous actions.
    /// </summary>
    public string? ActorId { get; set; }

    /// <summary>
    /// Gets or sets the type of actor that performed the action.
    /// Determines whether the action was performed by a user, system, or bot.
    /// </summary>
    public AuditLogActorType ActorType { get; set; }

    /// <summary>
    /// Gets or sets the type name of the entity that was affected by this action.
    /// Example: "User", "Guild", "ScheduledMessage", etc.
    /// </summary>
    public string? TargetType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the entity that was affected by this action.
    /// </summary>
    public string? TargetId { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild ID associated with this action.
    /// Null for system-wide or non-guild-specific actions.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets additional contextual information as a JSON string.
    /// Contains action-specific details such as changed values, parameters, etc.
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Gets or sets the IP address from which the action was performed.
    /// Primarily used for user actions through the web interface.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Gets or sets a correlation ID to group related audit log entries.
    /// Useful for tracing a series of actions that are part of the same operation.
    /// </summary>
    public string? CorrelationId { get; set; }
}
