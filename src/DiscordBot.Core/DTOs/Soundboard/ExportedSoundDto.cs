namespace DiscordBot.Core.DTOs.Soundboard;

/// <summary>
/// Represents metadata for a single sound in an export manifest.
/// </summary>
public class ExportedSoundDto
{
    /// <summary>
    /// Gets or sets the unique identifier of the sound.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the sound.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the filename of the sound in the export archive.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the original filename before export sanitization.
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration of the sound in seconds.
    /// </summary>
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long FileSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of times this sound has been played.
    /// </summary>
    public int PlayCount { get; set; }

    /// <summary>
    /// Gets or sets the Discord user ID who uploaded this sound.
    /// Null if the uploader is unknown.
    /// </summary>
    public string? UploadedById { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when this sound was uploaded (ISO8601 UTC format).
    /// </summary>
    public string UploadedAt { get; set; } = string.Empty;
}
