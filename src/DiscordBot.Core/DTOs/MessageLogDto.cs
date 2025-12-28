using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for message log information in listings and details views.
/// </summary>
public class MessageLogDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this log entry.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets Discord's unique message ID (snowflake).
    /// </summary>
    public ulong DiscordMessageId { get; set; }

    /// <summary>
    /// Gets or sets the ID of the user who authored the message.
    /// </summary>
    public ulong AuthorId { get; set; }

    /// <summary>
    /// Gets or sets the username of the message author for display purposes.
    /// </summary>
    public string? AuthorUsername { get; set; }

    /// <summary>
    /// Gets or sets the ID of the channel where the message was sent.
    /// </summary>
    public ulong ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the name of the channel for display purposes.
    /// </summary>
    public string? ChannelName { get; set; }

    /// <summary>
    /// Gets or sets the ID of the guild (server) where the message was sent.
    /// Null for direct messages.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the name of the guild for display purposes.
    /// </summary>
    public string? GuildName { get; set; }

    /// <summary>
    /// Gets or sets the source of the message (DirectMessage or ServerChannel).
    /// </summary>
    public MessageSource Source { get; set; }

    /// <summary>
    /// Gets or sets the content of the message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the message was originally sent on Discord.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets the timestamp in ISO 8601 format for client-side timezone conversion.
    /// </summary>
    public string TimestampUtcIso => DateTime.SpecifyKind(Timestamp, DateTimeKind.Utc).ToString("o");

    /// <summary>
    /// Gets or sets the timestamp when the message was logged to the database.
    /// </summary>
    public DateTime LoggedAt { get; set; }

    /// <summary>
    /// Gets the logged at timestamp in ISO 8601 format for client-side timezone conversion.
    /// </summary>
    public string LoggedAtUtcIso => DateTime.SpecifyKind(LoggedAt, DateTimeKind.Utc).ToString("o");

    /// <summary>
    /// Gets or sets whether the message has attachments (images, files, etc.).
    /// </summary>
    public bool HasAttachments { get; set; }

    /// <summary>
    /// Gets or sets whether the message has embeds (rich content).
    /// </summary>
    public bool HasEmbeds { get; set; }

    /// <summary>
    /// Gets or sets the ID of the message this message is replying to.
    /// Null if not a reply.
    /// </summary>
    public ulong? ReplyToMessageId { get; set; }
}
