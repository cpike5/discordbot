namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the category/sentiment of a moderator tag.
/// </summary>
public enum TagCategory
{
    /// <summary>
    /// Positive tag (Trusted, VIP, Verified, Helper).
    /// </summary>
    Positive = 0,

    /// <summary>
    /// Negative tag (Spammer, Troll, Repeat Offender, Under Review).
    /// </summary>
    Negative = 1,

    /// <summary>
    /// Neutral tag (no positive or negative connotation).
    /// </summary>
    Neutral = 2
}
