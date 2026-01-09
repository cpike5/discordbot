namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a single sound play event for analytics tracking.
/// Used to track when sounds are played, by whom, and in which guild.
/// </summary>
public class SoundPlayLog
{
    /// <summary>
    /// Gets or sets the unique identifier for this play log entry.
    /// Uses long (Int64) to support high-volume logging scenarios.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the sound that was played.
    /// </summary>
    public Guid SoundId { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where the sound was played.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID who played the sound.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the sound was played.
    /// Stored in UTC.
    /// </summary>
    public DateTime PlayedAt { get; set; }

    /// <summary>
    /// Navigation property for the sound that was played.
    /// </summary>
    public Sound? Sound { get; set; }
}
