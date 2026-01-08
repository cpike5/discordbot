using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Log entry for a Discord message.
/// Stores message content and metadata for analytics, auditing, and moderation purposes.
/// </summary>
public class MessageLog
{
    /// <summary>
    /// Unique identifier for this log entry.
    /// Uses long for high volume scenarios.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Discord's unique message ID (snowflake).
    /// </summary>
    public ulong DiscordMessageId { get; set; }

    /// <summary>
    /// ID of the user who authored the message.
    /// </summary>
    public ulong AuthorId { get; set; }

    /// <summary>
    /// ID of the channel where the message was sent.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Name of the channel where the message was sent.
    /// Null for direct messages and existing records without channel name.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    /// ID of the guild (server) where the message was sent.
    /// Null for direct messages.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Source of the message (DirectMessage or ServerChannel).
    /// </summary>
    public MessageSource Source { get; set; }

    /// <summary>
    /// Content of the message.
    /// Empty string if the message has no text content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the message was originally sent on Discord.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Timestamp when the message was logged to the database.
    /// </summary>
    public DateTime LoggedAt { get; set; }

    /// <summary>
    /// Whether the message has attachments (images, files, etc.).
    /// </summary>
    public bool HasAttachments { get; set; }

    /// <summary>
    /// Whether the message has embeds (rich content).
    /// </summary>
    public bool HasEmbeds { get; set; }

    /// <summary>
    /// ID of the message this message is replying to.
    /// Null if not a reply.
    /// </summary>
    public ulong? ReplyToMessageId { get; set; }

    /// <summary>
    /// Navigation property for the guild (nullable for DM messages).
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// Navigation property for the message author.
    /// </summary>
    public User? User { get; set; }
}
