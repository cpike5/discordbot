using DiscordBot.Core.Entities;

namespace DiscordBot.Core.DTOs.Soundboard;

/// <summary>
/// Result of a sound upload operation.
/// </summary>
public class SoundUploadResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the upload was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if upload failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the created sound entity.
    /// Null if upload failed.
    /// </summary>
    public Sound? Sound { get; set; }
}
