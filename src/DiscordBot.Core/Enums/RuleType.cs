namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the type of auto-moderation rule that triggered a flagged event.
/// </summary>
public enum RuleType
{
    /// <summary>
    /// Spam detection rule (message flooding, duplicate messages, mention abuse).
    /// </summary>
    Spam = 0,

    /// <summary>
    /// Content filtering rule (prohibited words, patterns, or phrases).
    /// </summary>
    Content = 1,

    /// <summary>
    /// Raid protection rule (mass joins, coordinated attacks).
    /// </summary>
    Raid = 2
}
