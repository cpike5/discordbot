using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a moderator tag definition for a guild.
/// </summary>
public class ModTag
{
    /// <summary>
    /// Unique identifier for this tag definition.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where this tag is defined.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Name of the tag (e.g., "Spammer", "Trusted", "Under Review").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Hex color code for displaying this tag (e.g., "#FF5733").
    /// </summary>
    public string Color { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of what this tag means.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Category of this tag (Positive, Negative, Neutral).
    /// </summary>
    public TagCategory Category { get; set; }

    /// <summary>
    /// Whether this tag was imported from a built-in template.
    /// </summary>
    public bool IsFromTemplate { get; set; }

    /// <summary>
    /// Timestamp when this tag was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild this tag belongs to.
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// Navigation property for user assignments of this tag.
    /// </summary>
    public ICollection<UserModTag> UserTags { get; set; } = new List<UserModTag>();
}
