using DiscordBot.Core.Entities;

namespace DiscordBot.Core.DTOs.Soundboard;

/// <summary>
/// Result of a sound playback operation.
/// </summary>
public class SoundPlayResult
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
    /// Gets or sets the sound entity that was played.
    /// Null if playback failed before fetching metadata.
    /// </summary>
    public Sound? Sound { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the sound was queued (vs. playing immediately).
    /// </summary>
    public bool WasQueued { get; set; }

    /// <summary>
    /// Gets or sets the queue position if the sound was queued.
    /// Null if sound is playing immediately.
    /// </summary>
    public int? QueuePosition { get; set; }
}
