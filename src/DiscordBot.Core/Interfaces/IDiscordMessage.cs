namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Abstraction over Discord.NET's SocketMessage to enable testability.
/// Wraps the non-virtual properties from Discord.NET's Socket types.
/// </summary>
public interface IDiscordMessage
{
    /// <summary>
    /// Gets the unique identifier for this message.
    /// </summary>
    ulong Id { get; }

    /// <summary>
    /// Gets the unique identifier of the message author.
    /// </summary>
    ulong AuthorId { get; }

    /// <summary>
    /// Gets a value indicating whether the message author is a bot.
    /// </summary>
    bool IsAuthorBot { get; }

    /// <summary>
    /// Gets a value indicating whether the message is a user message (not a system message).
    /// </summary>
    bool IsUserMessage { get; }

    /// <summary>
    /// Gets the unique identifier of the channel where the message was sent.
    /// </summary>
    ulong ChannelId { get; }

    /// <summary>
    /// Gets a value indicating whether the message was sent in a direct message channel.
    /// </summary>
    bool IsDirectMessage { get; }

    /// <summary>
    /// Gets the unique identifier of the guild (server) where the message was sent, if applicable.
    /// </summary>
    ulong? GuildId { get; }

    /// <summary>
    /// Gets the message content (text).
    /// </summary>
    string Content { get; }

    /// <summary>
    /// Gets the timestamp when the message was sent.
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets a value indicating whether the message has any attachments.
    /// </summary>
    bool HasAttachments { get; }

    /// <summary>
    /// Gets a value indicating whether the message has any embeds.
    /// </summary>
    bool HasEmbeds { get; }

    /// <summary>
    /// Gets the ID of the message this message is replying to, if applicable.
    /// </summary>
    ulong? ReplyToMessageId { get; }
}
