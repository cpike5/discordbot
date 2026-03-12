namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a VOX message playback entry in a user's history.
/// Tracks messages played via the VOX portal, supporting favorites and replay.
/// </summary>
public class VoxMessageHistory
{
    /// <summary>
    /// Unique identifier for this history record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Discord guild ID where the message was played.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Discord user ID who played the message.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// The VOX message text that was played.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The VOX clip group used (vox, fvox, or hgrunt).
    /// </summary>
    public string ClipGroup { get; set; } = string.Empty;

    /// <summary>
    /// The word gap in milliseconds used during playback.
    /// </summary>
    public int WordGapMs { get; set; }

    /// <summary>
    /// Whether this entry has been marked as a favorite by the user.
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Timestamp when the message was played (UTC).
    /// </summary>
    public DateTime PlayedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild.
    /// </summary>
    public Guild? Guild { get; set; }
}
