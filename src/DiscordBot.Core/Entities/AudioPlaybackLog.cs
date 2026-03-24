using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a single audio playback event for moderation tracking.
/// Logs when any audio feature (soundboard, TTS, VOX) is used, by whom, and in which guild/channel.
/// </summary>
public class AudioPlaybackLog
{
    /// <summary>
    /// Gets or sets the unique identifier for this playback log entry.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the Discord guild snowflake ID where the audio was played.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the Discord user snowflake ID who triggered the playback.
    /// </summary>
    public ulong UserId { get; set; }

    /// <summary>
    /// Gets or sets the type of audio feature used (Soundboard, TTS, or VOX).
    /// </summary>
    public AudioFeatureType FeatureType { get; set; }

    /// <summary>
    /// Gets or sets the content name describing what was played.
    /// For soundboard: the sound name. For TTS: a preview of the text. For VOX: the message.
    /// Truncated to a maximum of 200 characters.
    /// </summary>
    public string ContentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord voice channel snowflake ID where the audio was played.
    /// May be null if channel information is unavailable.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the audio was played.
    /// Stored in UTC.
    /// </summary>
    public DateTime PlayedAt { get; set; }

    /// <summary>
    /// Navigation property for the guild where the audio was played.
    /// </summary>
    public Guild? Guild { get; set; }
}
