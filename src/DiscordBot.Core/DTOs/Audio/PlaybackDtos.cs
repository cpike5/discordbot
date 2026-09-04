using System.Text.Json.Serialization;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// DTO for playback started event when a sound begins playing.
/// </summary>
public class PlaybackStartedDto
{
    /// <summary>
    /// Gets or sets the guild ID where playback started.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the sound ID being played.
    /// </summary>
    public Guid SoundId { get; set; }

    /// <summary>
    /// Gets or sets the sound name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration of the sound in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the display name of the user who requested playback.
    /// Null if the requester is unknown.
    /// </summary>
    public string? RequestedByDisplayName { get; set; }

    /// <summary>
    /// Gets or sets the playback source type (e.g., "Soundboard", "TTS").
    /// Null defaults to "Soundboard" for backwards compatibility.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when playback started.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for playback progress updates during sound playback.
/// </summary>
public class PlaybackProgressDto
{
    /// <summary>
    /// Gets or sets the guild ID where playback is occurring.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the sound ID being played.
    /// </summary>
    public Guid SoundId { get; set; }

    /// <summary>
    /// Gets or sets the current position in seconds.
    /// </summary>
    public double PositionSeconds { get; set; }

    /// <summary>
    /// Gets or sets the total duration in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the progress update.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO for playback finished event when a sound completes.
/// </summary>
public class PlaybackFinishedDto
{
    /// <summary>
    /// Gets or sets the guild ID where playback finished.
    /// </summary>
    [JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowReadingFromString)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the sound ID that finished playing.
    /// </summary>
    public Guid SoundId { get; set; }

    /// <summary>
    /// Gets or sets whether playback was cancelled (vs. completed naturally).
    /// </summary>
    public bool WasCancelled { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when playback finished.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
