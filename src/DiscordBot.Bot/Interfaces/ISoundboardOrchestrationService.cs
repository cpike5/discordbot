using DiscordBot.Core.DTOs.Soundboard;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Service interface for soundboard orchestration operations.
/// Consolidates upload pipeline, play orchestration, and delete orchestration.
/// </summary>
public interface ISoundboardOrchestrationService
{
    /// <summary>
    /// Orchestrates the complete sound upload pipeline.
    /// Validates audio globally/per-guild, validates file format, checks limits,
    /// saves file to disk, gets duration, creates entity, and notifies via SignalR.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="fileName">Original uploaded file name (for format validation).</param>
    /// <param name="soundName">Display name for the sound.</param>
    /// <param name="fileStream">Stream containing the audio file data.</param>
    /// <param name="fileSizeBytes">Size of the file in bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing success status, error message, and created sound metadata.</returns>
    Task<SoundUploadResult> UploadSoundAsync(
        ulong guildId,
        string fileName,
        string soundName,
        Stream fileStream,
        long fileSizeBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Orchestrates sound playback with full validation and logging.
    /// Validates audio globally/per-guild, checks bot connection, fetches metadata,
    /// verifies file exists, plays sound, and logs play event.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="soundId">Unique identifier of the sound to play.</param>
    /// <param name="userId">Discord user ID who triggered playback (for logging).</param>
    /// <param name="queueEnabled">Whether to queue the sound or replace current playback.</param>
    /// <param name="filter">Optional audio filter to apply during playback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing success status, error message, sound metadata, and queue information.</returns>
    Task<SoundPlayResult> PlaySoundAsync(
        ulong guildId,
        Guid soundId,
        ulong userId,
        bool queueEnabled,
        AudioFilter filter = AudioFilter.None,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Orchestrates sound deletion.
    /// Fetches sound metadata, deletes file from disk, deletes database record, and notifies via SignalR.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="soundId">Unique identifier of the sound to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing success status, error message, and deleted sound name.</returns>
    Task<SoundDeleteResult> DeleteSoundAsync(
        ulong guildId,
        Guid soundId,
        CancellationToken cancellationToken = default);
}
