namespace DiscordBot.Core.DTOs.Soundboard;

/// <summary>
/// Result of a sound deletion operation.
/// </summary>
public class SoundDeleteResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the deletion was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if deletion failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the name of the deleted sound.
    /// Null if sound was not found.
    /// </summary>
    public string? DeletedSoundName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the file existed on disk and was deleted.
    /// False if the database record existed but the file was already missing.
    /// </summary>
    public bool FileDeleted { get; set; }
}
