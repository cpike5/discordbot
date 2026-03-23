namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a category for organizing sounds within a guild's soundboard.
/// Categories are guild-scoped and admin-managed.
/// </summary>
public class SoundCategory
{
    /// <summary>
    /// Unique identifier for this category (primary key).
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where this category belongs.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Display name for the category (e.g., "Meme", "Game", "Intro").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Sort order for displaying categories. Lower values appear first.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Timestamp when this category was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild this category belongs to.
    /// </summary>
    public Guild? Guild { get; set; }

    /// <summary>
    /// Navigation property for the sounds assigned to this category.
    /// </summary>
    public ICollection<Sound> Sounds { get; set; } = new List<Sound>();
}
