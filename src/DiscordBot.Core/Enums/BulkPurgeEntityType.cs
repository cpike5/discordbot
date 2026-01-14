namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the entity types that can be bulk purged.
/// </summary>
public enum BulkPurgeEntityType
{
    /// <summary>
    /// Message logs from Discord channels.
    /// </summary>
    Messages = 1,

    /// <summary>
    /// System audit logs.
    /// </summary>
    AuditLogs = 2,

    /// <summary>
    /// Command execution logs.
    /// </summary>
    CommandLogs = 3,

    /// <summary>
    /// Moderation case records.
    /// </summary>
    ModerationCases = 4
}
