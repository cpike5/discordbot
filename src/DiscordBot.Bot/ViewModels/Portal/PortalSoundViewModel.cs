namespace DiscordBot.Bot.ViewModels.Portal;

/// <summary>
/// View model for a sound in the Portal Soundboard interface.
/// Contains only essential properties needed for display and playback.
/// </summary>
public record PortalSoundViewModel
{
    /// <summary>
    /// Gets the unique identifier for this sound.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the display name for the sound.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of times this sound has been played.
    /// </summary>
    public int PlayCount { get; init; }

    /// <summary>
    /// Gets the audio duration in seconds.
    /// </summary>
    public double DurationSeconds { get; init; }

    /// <summary>
    /// Gets the Discord user ID of the uploader as a string (for JS snowflake safety).
    /// Null for filesystem-discovered sounds or sounds uploaded before tracking was added.
    /// </summary>
    public string? UploadedById { get; init; }

    /// <summary>
    /// Gets the timestamp when the sound was uploaded (UTC).
    /// </summary>
    public DateTime? UploadedAt { get; init; }
}
