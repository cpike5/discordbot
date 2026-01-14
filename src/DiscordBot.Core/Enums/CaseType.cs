namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the type of moderation action taken against a user.
/// </summary>
public enum CaseType
{
    /// <summary>
    /// Formal warning issued to a user.
    /// </summary>
    Warn = 0,

    /// <summary>
    /// User was kicked from the guild.
    /// </summary>
    Kick = 1,

    /// <summary>
    /// User was banned from the guild (permanent or temporary).
    /// </summary>
    Ban = 2,

    /// <summary>
    /// User was muted/timed out.
    /// </summary>
    Mute = 3,

    /// <summary>
    /// Informational note only (no enforcement action).
    /// </summary>
    Note = 4,

    /// <summary>
    /// User was unbanned from the guild.
    /// </summary>
    Unban = 5
}
