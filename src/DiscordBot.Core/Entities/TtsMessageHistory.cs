namespace DiscordBot.Core.Entities;

/// <summary>
/// Represents a TTS message playback entry in a user's history.
/// Tracks messages played via the TTS portal, supporting favorites and replay.
/// </summary>
public class TtsMessageHistory
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
    /// The TTS message text that was played.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The voice name used for synthesis (e.g., "en-US-AriaNeural").
    /// </summary>
    public string VoiceName { get; set; } = string.Empty;

    /// <summary>
    /// The optional voice style used (e.g., "cheerful").
    /// </summary>
    public string? Style { get; set; }

    /// <summary>
    /// The speech rate multiplier used during playback.
    /// </summary>
    public decimal Speed { get; set; } = 1.0m;

    /// <summary>
    /// The pitch adjustment used during playback.
    /// </summary>
    public decimal Pitch { get; set; } = 1.0m;

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
