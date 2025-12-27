namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the category of an audit log entry.
/// </summary>
public enum AuditLogCategory
{
    /// <summary>
    /// User-related actions (e.g., login, profile updates, ban, kick).
    /// </summary>
    User = 1,

    /// <summary>
    /// Guild-related actions (e.g., guild settings, channel management).
    /// </summary>
    Guild = 2,

    /// <summary>
    /// Configuration-related actions (e.g., bot settings, feature toggles).
    /// </summary>
    Configuration = 3,

    /// <summary>
    /// Security-related actions (e.g., permission changes, role modifications).
    /// </summary>
    Security = 4,

    /// <summary>
    /// Command execution actions (e.g., slash command usage).
    /// </summary>
    Command = 5,

    /// <summary>
    /// Message-related actions (e.g., message deletion, editing).
    /// </summary>
    Message = 6,

    /// <summary>
    /// System-level actions (e.g., bot startup, shutdown, errors).
    /// </summary>
    System = 7
}
