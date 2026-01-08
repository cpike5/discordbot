namespace DiscordBot.Core.Enums;

/// <summary>
/// Defines the types of user activity events tracked for anonymous analytics.
/// These events capture activity metadata without storing any message content.
/// </summary>
public enum ActivityEventType
{
    /// <summary>
    /// User sent a message in a channel.
    /// </summary>
    Message = 1,

    /// <summary>
    /// User added a reaction to a message.
    /// </summary>
    Reaction = 2,

    /// <summary>
    /// User joined a voice channel.
    /// </summary>
    VoiceJoin = 3,

    /// <summary>
    /// User left a voice channel.
    /// </summary>
    VoiceLeave = 4,

    /// <summary>
    /// User replied to a message.
    /// </summary>
    Reply = 5,

    /// <summary>
    /// User mentioned another user or role.
    /// </summary>
    Mention = 6,

    /// <summary>
    /// User shared an attachment (image, file, etc.).
    /// </summary>
    Attachment = 7,

    /// <summary>
    /// User used a slash command.
    /// </summary>
    SlashCommand = 8
}
