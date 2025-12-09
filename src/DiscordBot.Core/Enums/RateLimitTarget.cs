namespace DiscordBot.Core.Enums;

/// <summary>
/// Defines the target scope for rate limiting.
/// </summary>
public enum RateLimitTarget
{
    /// <summary>
    /// Rate limit applies per user across all guilds.
    /// </summary>
    User,

    /// <summary>
    /// Rate limit applies per guild (all users in the guild share the limit).
    /// </summary>
    Guild,

    /// <summary>
    /// Rate limit applies globally across all users and guilds.
    /// </summary>
    Global
}
