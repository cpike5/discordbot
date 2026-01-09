namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents an audio file uploaded or discovered for the soundboard feature.
/// </summary>
public class Sound
{
    /// <summary>
    /// Unique identifier for this sound (primary key).
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Discord guild snowflake ID where this sound belongs.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Display name for the sound, used in autocomplete and listings.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Actual filename on disk in the soundboard storage directory.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Audio duration in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Discord user snowflake ID who uploaded this sound.
    /// Null if the sound was discovered from the filesystem.
    /// </summary>
    public ulong? UploadedById { get; set; }

    /// <summary>
    /// Timestamp when the sound was added to the database (UTC).
    /// </summary>
    public DateTime UploadedAt { get; set; }

    /// <summary>
    /// Number of times this sound has been played.
    /// </summary>
    public int PlayCount { get; set; }

    /// <summary>
    /// Navigation property for the guild this sound belongs to.
    /// </summary>
    public Guild? Guild { get; set; }
}
