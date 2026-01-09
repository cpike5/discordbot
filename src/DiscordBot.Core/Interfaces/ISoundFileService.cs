namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing sound file storage on disk.
/// Handles file I/O operations, path resolution, and audio file validation.
/// </summary>
public interface ISoundFileService
{
    /// <summary>
    /// Saves a sound file to disk in the guild's sound directory.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="fileName">Name of the file including extension.</param>
    /// <param name="fileStream">Stream containing the file data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Creates the guild directory if it doesn't exist.
    /// Overwrites existing files with the same name.
    /// </remarks>
    Task SaveSoundFileAsync(ulong guildId, string fileName, Stream fileStream, CancellationToken ct = default);

    /// <summary>
    /// Deletes a sound file from disk.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="fileName">Name of the file to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the file existed and was deleted, false if the file did not exist.</returns>
    Task<bool> DeleteSoundFileAsync(ulong guildId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Resolves the full file system path for a sound file.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="fileName">Name of the file.</param>
    /// <returns>Absolute path to the sound file.</returns>
    /// <remarks>
    /// This is a synchronous operation that does not perform I/O.
    /// Does not verify whether the file exists.
    /// </remarks>
    string GetSoundFilePath(ulong guildId, string fileName);

    /// <summary>
    /// Checks if a sound file exists on disk.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="fileName">Name of the file to check.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    bool SoundFileExists(ulong guildId, string fileName);

    /// <summary>
    /// Discovers all sound files in a guild's directory.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of file names (without paths) found in the guild directory.</returns>
    /// <remarks>
    /// Returns an empty collection if the guild directory does not exist.
    /// Only returns files with valid audio extensions.
    /// </remarks>
    Task<IReadOnlyList<string>> DiscoverSoundFilesAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Gets the duration of an audio file in seconds.
    /// </summary>
    /// <param name="filePath">Absolute path to the audio file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Duration in seconds, or 0 if the file cannot be read.</returns>
    /// <remarks>
    /// Uses audio library to parse file metadata.
    /// Returns 0 for invalid or corrupted files.
    /// </remarks>
    Task<double> GetAudioDurationAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// Validates whether a file name has a supported audio format extension.
    /// </summary>
    /// <param name="fileName">Name of the file to validate.</param>
    /// <returns>True if the file extension is a supported audio format, false otherwise.</returns>
    /// <remarks>
    /// Supported formats: .mp3, .wav, .ogg, .m4a
    /// Check is case-insensitive.
    /// </remarks>
    bool IsValidAudioFormat(string fileName);

    /// <summary>
    /// Ensures the guild's sound directory exists on disk.
    /// Creates the directory if it doesn't exist.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task EnsureGuildDirectoryExistsAsync(ulong guildId, CancellationToken ct = default);
}
