namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the type of actor that performed an audited action.
/// </summary>
public enum AuditLogActorType
{
    /// <summary>
    /// Action was performed by a user (authenticated human).
    /// </summary>
    User = 1,

    /// <summary>
    /// Action was performed by the system (automated process, scheduled task).
    /// </summary>
    System = 2,

    /// <summary>
    /// Action was performed by the Discord bot itself.
    /// </summary>
    Bot = 3
}
