using DiscordBot.Core.Entities;

namespace DiscordBot.Core.DTOs.Tts;

/// <summary>
/// Result of a TTS playback operation.
/// </summary>
public class TtsPlaybackResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the playback was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if playback failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the duration of the audio in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the logged TTS message entity.
    /// Null if playback failed before logging.
    /// </summary>
    public TtsMessage? LoggedMessage { get; set; }
}
