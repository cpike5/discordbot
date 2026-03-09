namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a user's favorited sound within a guild's soundboard.
/// Allows users to bookmark sounds for quick access in the portal.
/// </summary>
public class UserSoundFavorite
{
    /// <summary>
    /// Unique identifier for this favorite record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Discord user ID who favorited the sound.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Discord guild ID where the sound belongs.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Unique identifier of the favorited sound.
    /// </summary>
    public Guid SoundId { get; set; }

    /// <summary>
    /// Timestamp when the sound was favorited (UTC).
    /// </summary>
    public DateTime FavoritedAt { get; set; }

    /// <summary>
    /// Navigation property for the favorited sound.
    /// </summary>
    public Sound? Sound { get; set; }
}
