using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs.Soundboard;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for soundboard orchestration operations.
/// Consolidates upload pipeline, play orchestration, and delete orchestration.
/// </summary>
public class SoundboardOrchestrationService : ISoundboardOrchestrationService
{
    private readonly ISoundService _soundService;
    private readonly ISoundFileService _soundFileService;
    private readonly IPlaybackService _playbackService;
    private readonly IAudioService _audioService;
    private readonly IGuildAudioSettingsService _audioSettingsService;
    private readonly ISettingsService _settingsService;
    private readonly IAudioNotifier _audioNotifier;
    private readonly ILogger<SoundboardOrchestrationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoundboardOrchestrationService"/> class.
    /// </summary>
    /// <param name="soundService">The sound service for metadata operations.</param>
    /// <param name="soundFileService">The sound file service for file operations.</param>
    /// <param name="playbackService">The playback service for audio control.</param>
    /// <param name="audioService">The audio service for voice connections.</param>
    /// <param name="audioSettingsService">The audio settings service.</param>
    /// <param name="settingsService">The bot-level settings service.</param>
    /// <param name="audioNotifier">The audio notifier for real-time updates.</param>
    /// <param name="logger">The logger.</param>
    public SoundboardOrchestrationService(
        ISoundService soundService,
        ISoundFileService soundFileService,
        IPlaybackService playbackService,
        IAudioService audioService,
        IGuildAudioSettingsService audioSettingsService,
        ISettingsService settingsService,
        IAudioNotifier audioNotifier,
        ILogger<SoundboardOrchestrationService> logger)
    {
        _soundService = soundService;
        _soundFileService = soundFileService;
        _playbackService = playbackService;
        _audioService = audioService;
        _audioSettingsService = audioSettingsService;
        _settingsService = settingsService;
        _audioNotifier = audioNotifier;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SoundUploadResult> UploadSoundAsync(
        ulong guildId,
        string fileName,
        string soundName,
        Stream fileStream,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        // Validate parameters
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(soundName);

        using var activity = BotActivitySource.StartServiceActivity("soundboard_orchestration", "upload", guildId: guildId);

        _logger.LogInformation("Upload sound request for guild {GuildId}, name {SoundName}", guildId, soundName);

        string? savedFilePath = null;

        try
        {
            // Check if audio is globally enabled at the bot level
            if (!await IsAudioGloballyEnabledAsync(cancellationToken))
            {
                _logger.LogWarning("Audio features globally disabled - rejecting upload for guild {GuildId}", guildId);
                BotActivitySource.SetSuccess(activity);
                return new SoundUploadResult
                {
                    Success = false,
                    ErrorMessage = "Audio features have been disabled by an administrator."
                };
            }

            // Check if audio is enabled for this guild
            var audioSettings = await _audioSettingsService.GetSettingsAsync(guildId, cancellationToken);
            if (audioSettings == null || !audioSettings.AudioEnabled)
            {
                _logger.LogWarning("Audio not enabled for guild {GuildId}", guildId);
                BotActivitySource.SetSuccess(activity);
                return new SoundUploadResult
                {
                    Success = false,
                    ErrorMessage = "Audio is not enabled for this guild. Enable audio in the guild settings before uploading sounds."
                };
            }

            // Validate audio format
            if (!_soundFileService.IsValidAudioFormat(fileName))
            {
                _logger.LogWarning("Invalid audio format {FileName} for guild {GuildId}", fileName, guildId);
                BotActivitySource.SetSuccess(activity);
                return new SoundUploadResult
                {
                    Success = false,
                    ErrorMessage = "Invalid audio format. Supported formats: .mp3, .wav, .ogg, .m4a"
                };
            }

            // Check sound count limit
            if (!await _soundService.ValidateSoundCountLimitAsync(guildId, cancellationToken))
            {
                var currentCount = await _soundService.GetSoundCountAsync(guildId, cancellationToken);
                _logger.LogWarning("Sound count limit reached for guild {GuildId} (current: {CurrentCount}, max: {MaxSounds})",
                    guildId, currentCount, audioSettings.MaxSoundsPerGuild);
                BotActivitySource.SetSuccess(activity);
                return new SoundUploadResult
                {
                    Success = false,
                    ErrorMessage = $"This guild has reached the maximum number of sounds ({audioSettings.MaxSoundsPerGuild}). Please delete some sounds before adding new ones."
                };
            }

            // Check storage limit
            if (!await _soundService.ValidateStorageLimitAsync(guildId, fileSizeBytes, cancellationToken))
            {
                var currentStorage = await _soundService.GetStorageUsedAsync(guildId, cancellationToken);
                var maxStorageMB = audioSettings.MaxStorageBytes / (1024 * 1024);
                var currentStorageMB = currentStorage / (1024.0 * 1024.0);
                _logger.LogWarning("Storage limit would be exceeded for guild {GuildId} (current: {CurrentMB:F2} MB, file: {FileMB:F2} MB, max: {MaxMB} MB)",
                    guildId, currentStorageMB, fileSizeBytes / (1024.0 * 1024.0), maxStorageMB);
                BotActivitySource.SetSuccess(activity);
                return new SoundUploadResult
                {
                    Success = false,
                    ErrorMessage = $"Adding this file would exceed the storage limit of {maxStorageMB} MB. Current usage: {currentStorageMB:F2} MB."
                };
            }

            // Check for duplicate name
            var existingSound = await _soundService.GetByNameAsync(soundName, guildId, cancellationToken);
            if (existingSound != null)
            {
                _logger.LogWarning("Duplicate sound name {SoundName} for guild {GuildId}", soundName, guildId);
                BotActivitySource.SetSuccess(activity);
                return new SoundUploadResult
                {
                    Success = false,
                    ErrorMessage = $"A sound with the name '{soundName}' already exists in this guild."
                };
            }

            // Generate unique filename with extension
            var extension = Path.GetExtension(fileName);
            var uniqueFileName = $"{Guid.NewGuid()}{extension}";

            // Save file to disk
            await _soundFileService.EnsureGuildDirectoryExistsAsync(guildId, cancellationToken);
            await _soundFileService.SaveSoundFileAsync(guildId, uniqueFileName, fileStream, cancellationToken);
            savedFilePath = _soundFileService.GetSoundFilePath(guildId, uniqueFileName);

            // Get audio duration
            var duration = await _soundFileService.GetAudioDurationAsync(savedFilePath, cancellationToken);

            // Create sound entity
            var sound = new Sound
            {
                GuildId = guildId,
                Name = soundName,
                FileName = uniqueFileName,
                FileSizeBytes = fileSizeBytes,
                DurationSeconds = duration,
                UploadedAt = DateTime.UtcNow
            };

            var createdSound = await _soundService.CreateSoundAsync(sound, cancellationToken);

            _logger.LogInformation("Successfully uploaded sound {SoundName} ({SoundId}) for guild {GuildId}",
                createdSound.Name, createdSound.Id, guildId);

            // Broadcast to other portal viewers via SignalR
            await _audioNotifier.NotifySoundUploadedAsync(
                guildId,
                createdSound.Id,
                createdSound.Name,
                createdSound.PlayCount,
                cancellationToken);

            BotActivitySource.SetSuccess(activity);

            return new SoundUploadResult
            {
                Success = true,
                Sound = createdSound
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading sound {SoundName} for guild {GuildId}", soundName, guildId);
            BotActivitySource.RecordException(activity, ex);

            // Attempt to clean up orphaned file if it was saved but DB creation failed
            if (!string.IsNullOrEmpty(savedFilePath))
            {
                try
                {
                    if (File.Exists(savedFilePath))
                    {
                        File.Delete(savedFilePath);
                        _logger.LogInformation("Cleaned up orphaned file {FilePath} after upload failure", savedFilePath);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to clean up orphaned file {FilePath}", savedFilePath);
                }
            }

            return new SoundUploadResult
            {
                Success = false,
                ErrorMessage = "An error occurred while uploading the sound. Please try again."
            };
        }
    }

    /// <inheritdoc/>
    public async Task<SoundPlayResult> PlaySoundAsync(
        ulong guildId,
        Guid soundId,
        ulong userId,
        bool queueEnabled,
        AudioFilter filter = AudioFilter.None,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity("soundboard_orchestration", "play", guildId: guildId, entityId: soundId.ToString());

        _logger.LogInformation("Play sound request for sound {SoundId} in guild {GuildId} by user {UserId}",
            soundId, guildId, userId);

        try
        {
            // Check if audio is globally enabled at the bot level
            if (!await IsAudioGloballyEnabledAsync(cancellationToken))
            {
                _logger.LogWarning("Audio features globally disabled - rejecting play for guild {GuildId}", guildId);
                BotActivitySource.SetSuccess(activity);
                return new SoundPlayResult
                {
                    Success = false,
                    ErrorMessage = "Audio features have been disabled by an administrator."
                };
            }

            // Check if audio is enabled for this guild
            var audioSettings = await _audioSettingsService.GetSettingsAsync(guildId, cancellationToken);
            if (audioSettings == null || !audioSettings.AudioEnabled)
            {
                _logger.LogWarning("Audio not enabled for guild {GuildId}", guildId);
                BotActivitySource.SetSuccess(activity);
                return new SoundPlayResult
                {
                    Success = false,
                    ErrorMessage = "Audio is not enabled for this guild. Enable audio in the guild settings before playing sounds."
                };
            }

            // Check if bot is connected to voice
            if (!_audioService.IsConnected(guildId))
            {
                _logger.LogWarning("Bot not connected to voice channel in guild {GuildId}", guildId);
                BotActivitySource.SetSuccess(activity);
                return new SoundPlayResult
                {
                    Success = false,
                    ErrorMessage = "The bot must be connected to a voice channel before playing sounds."
                };
            }

            // Get sound metadata
            var sound = await _soundService.GetByIdAsync(soundId, guildId, cancellationToken);
            if (sound == null)
            {
                _logger.LogWarning("Sound {SoundId} not found in guild {GuildId}", soundId, guildId);
                BotActivitySource.SetSuccess(activity);
                return new SoundPlayResult
                {
                    Success = false,
                    ErrorMessage = "The requested sound does not exist or does not belong to this guild."
                };
            }

            // Verify file exists on disk
            if (!_soundFileService.SoundFileExists(guildId, sound.FileName))
            {
                _logger.LogError("Sound file missing for sound {SoundId} in guild {GuildId}", soundId, guildId);
                BotActivitySource.SetSuccess(activity);
                return new SoundPlayResult
                {
                    Success = false,
                    ErrorMessage = "The sound exists in the database but the file is missing from storage.",
                    Sound = sound
                };
            }

            // Determine if sound will be queued based on current playback state
            var wasPlaying = _playbackService.IsPlaying(guildId);
            var queueLengthBefore = _playbackService.GetQueueLength(guildId);
            bool willBeQueued = queueEnabled && wasPlaying;
            int? queuePosition = willBeQueued ? queueLengthBefore + 1 : null;

            // Play the sound
            await _playbackService.PlayAsync(guildId, sound, queueEnabled, filter, cancellationToken);
            _logger.LogInformation("Successfully started playback of sound {SoundName} ({SoundId}) in guild {GuildId}",
                sound.Name, sound.Id, guildId);

            // Log play event (fire-and-forget - don't block on logging)
            _ = _soundService.LogPlayAsync(sound.Id, guildId, userId, cancellationToken);

            BotActivitySource.SetSuccess(activity);

            return new SoundPlayResult
            {
                Success = true,
                Sound = sound,
                WasQueued = willBeQueued,
                QueuePosition = queuePosition
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to play sound {SoundId} in guild {GuildId}", soundId, guildId);
            BotActivitySource.RecordException(activity, ex);
            return new SoundPlayResult
            {
                Success = false,
                ErrorMessage = "An error occurred while playing the sound. Please try again.",
                Sound = null
            };
        }
    }

    /// <inheritdoc/>
    public async Task<SoundDeleteResult> DeleteSoundAsync(
        ulong guildId,
        Guid soundId,
        CancellationToken cancellationToken = default)
    {
        using var activity = BotActivitySource.StartServiceActivity("soundboard_orchestration", "delete", guildId: guildId, entityId: soundId.ToString());

        _logger.LogInformation("Delete sound request for sound {SoundId} in guild {GuildId}", soundId, guildId);

        try
        {
            // Get sound to verify it exists and get filename
            var sound = await _soundService.GetByIdAsync(soundId, guildId, cancellationToken);
            if (sound == null)
            {
                _logger.LogWarning("Sound {SoundId} not found for guild {GuildId}", soundId, guildId);
                BotActivitySource.SetSuccess(activity);
                return new SoundDeleteResult
                {
                    Success = false,
                    ErrorMessage = "Sound not found."
                };
            }

            // Delete the physical file first
            var fileDeleted = await _soundFileService.DeleteSoundFileAsync(
                guildId,
                sound.FileName,
                cancellationToken);

            if (!fileDeleted)
            {
                _logger.LogWarning("File {FileName} not found on disk for sound {SoundId}",
                    sound.FileName, soundId);
            }

            // Delete the database record
            var dbDeleted = await _soundService.DeleteSoundAsync(soundId, guildId, cancellationToken);

            if (dbDeleted)
            {
                _logger.LogInformation("Successfully deleted sound {SoundId} ({Name})",
                    soundId, sound.Name);

                // Broadcast deletion to portal viewers via SignalR
                await _audioNotifier.NotifySoundDeletedAsync(guildId, soundId, cancellationToken);

                BotActivitySource.SetSuccess(activity);

                return new SoundDeleteResult
                {
                    Success = true,
                    DeletedSoundName = sound.Name,
                    FileDeleted = fileDeleted
                };
            }
            else
            {
                _logger.LogWarning("Failed to delete sound {SoundId} from database", soundId);
                BotActivitySource.SetSuccess(activity);
                return new SoundDeleteResult
                {
                    Success = false,
                    ErrorMessage = "Failed to delete sound from database."
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting sound {SoundId} for guild {GuildId}",
                soundId, guildId);
            BotActivitySource.RecordException(activity, ex);
            return new SoundDeleteResult
            {
                Success = false,
                ErrorMessage = "An error occurred while deleting the sound. Please try again."
            };
        }
    }

    /// <summary>
    /// Checks if audio features are globally enabled at the bot level.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if audio is globally enabled, false otherwise.</returns>
    private async Task<bool> IsAudioGloballyEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _settingsService.GetSettingValueAsync<bool?>("Features:AudioEnabled", cancellationToken) ?? true;
    }
}
