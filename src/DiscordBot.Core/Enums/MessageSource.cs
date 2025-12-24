namespace DiscordBot.Core.Enums;

/// <summary>
/// Represents the source/origin of a Discord message.
/// </summary>
public enum MessageSource
{
    /// <summary>
    /// Message sent in a direct message (DM) channel.
    /// </summary>
    DirectMessage = 1,

    /// <summary>
    /// Message sent in a server (guild) channel.
    /// </summary>
    ServerChannel = 2
}
