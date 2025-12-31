namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a private moderator note about a user.
/// </summary>
public class ModNote
{
    /// <summary>
    /// Unique identifier for this mod note.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where this note was created.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the user this note is about.
    /// </summary>
    public ulong TargetUserId { get; set; }

    /// <summary>
    /// Discord user snowflake ID of the moderator who created this note.
    /// </summary>
    public ulong AuthorUserId { get; set; }

    /// <summary>
    /// Content of the moderator note.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this note was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild this note belongs to.
    /// </summary>
    public Guild? Guild { get; set; }
}
