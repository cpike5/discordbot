namespace DiscordBot.Core.Enums;

/// <summary>
/// Types of user activity events tracked for analytics.
/// These events capture metadata only, not content.
/// </summary>
public enum ActivityEventType
{
    /// <summary>User sent a message in a channel.</summary>
    Message = 0,

    /// <summary>User added a reaction to a message.</summary>
    Reaction = 1,

    /// <summary>User joined a voice channel.</summary>
    VoiceJoin = 2,

    /// <summary>User left a voice channel.</summary>
    VoiceLeave = 3,

    /// <summary>User joined the guild.</summary>
    GuildJoin = 4,

    /// <summary>User left the guild.</summary>
    GuildLeave = 5
}
