using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for mod note information.
/// </summary>
public class ModNoteDto
{
    /// <summary>
    /// Gets or sets the unique identifier for this note.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where this note exists.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the user this note is about.
    /// </summary>
    public ulong TargetUserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the target user (resolved from Discord).
    /// </summary>
    public string TargetUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the moderator who created this note.
    /// </summary>
    public ulong AuthorUserId { get; set; }

    /// <summary>
    /// Gets or sets the username of the note author (resolved from Discord).
    /// </summary>
    public string AuthorUsername { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content of the note.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this note was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Data transfer object for creating a new moderator note.
/// </summary>
public class ModNoteCreateDto
{
    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where this note will be created.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the user this note is about.
    /// </summary>
    public ulong TargetUserId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID of the moderator creating this note.
    /// </summary>
    public ulong AuthorUserId { get; set; }

    /// <summary>
    /// Gets or sets the content of the note.
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
