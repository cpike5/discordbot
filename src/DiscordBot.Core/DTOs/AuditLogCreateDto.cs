using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for creating a new audit log entry.
/// Contains all required and optional fields for logging an action.
/// </summary>
public class AuditLogCreateDto
{
    /// <summary>
    /// Gets or sets the category of the audit log entry.
    /// </summary>
    public AuditLogCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the specific action that was performed.
    /// </summary>
    public AuditLogAction Action { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the actor who performed the action.
    /// Optional for anonymous or system actions.
    /// </summary>
    public string? ActorId { get; set; }

    /// <summary>
    /// Gets or sets the type of actor that performed the action.
    /// </summary>
    public AuditLogActorType ActorType { get; set; }

    /// <summary>
    /// Gets or sets the type name of the entity that was affected.
    /// Example: "User", "Guild", "ScheduledMessage", etc.
    /// </summary>
    public string? TargetType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the entity that was affected.
    /// </summary>
    public string? TargetId { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild ID associated with this action.
    /// Null for system-wide or non-guild-specific actions.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets additional contextual information as a JSON string.
    /// Should contain action-specific details such as changed values, parameters, etc.
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
