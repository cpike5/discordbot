using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the entity types that can be bulk purged.
/// </summary>
public enum BulkPurgeEntityType
{
    /// <summary>
    /// Message logs from Discord channels.
    /// </summary>
    [Display(Name = "Message logs", Description = "Discord message history")]
    Messages = 1,

    /// <summary>
    /// System audit logs.
    /// </summary>
    [Display(Name = "Audit logs", Description = "System audit trail")]
    AuditLogs = 2,

    /// <summary>
    /// Command execution logs.
    /// </summary>
    [Display(Name = "Command logs", Description = "Command execution logs")]
    CommandLogs = 3,

    /// <summary>
    /// Moderation case records.
    /// </summary>
    [Display(Name = "Moderation cases", Description = "Moderation case records")]
    ModerationCases = 4
}
