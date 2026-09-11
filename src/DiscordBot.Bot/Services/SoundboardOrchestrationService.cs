using Discord.WebSocket;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Constants;
using DiscordBot.Core.DTOs;
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
    private readonly DiscordSocketClient _discordClient;
    private readonly IAudioModerationLogService _audioModerationLogService;
    private readonly ILogger<SoundboardOrchestrationService> _logger;

    /// <summary>
    /// The charge seam, or null when the currency feature is switched off. With
    /// <c>Currency:Enabled</c> false nothing registers <see cref="IChargeService"/>, so this is
    /// null and every sound plays free — which is the feature's rollback path.
    /// </summary>
    private readonly IChargeService? _chargeService;

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
    /// <param name="discordClient">The Discord socket client for resolving user display names.</param>
    /// <param name="audioModerationLogService">The audio moderation log service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="chargeService">
    /// The currency charge seam, or null when <c>Currency:Enabled</c> is false. Optional so the
    /// soundboard still constructs with the currency feature switched off.
    /// </param>
    public SoundboardOrchestrationService(
        ISoundService soundService,
        ISoundFileService soundFileService,
        IPlaybackService playbackService,
        IAudioService audioService,
        IGuildAudioSettingsService audioSettingsService,
        ISettingsService settingsService,
        IAudioNotifier audioNotifier,
        DiscordSocketClient discordClient,
        IAudioModerationLogService audioModerationLogService,
        ILogger<SoundboardOrchestrationService> logger,
        IChargeService? chargeService = null)
    {
        _soundService = soundService;
        _soundFileService = soundFileService;
        _playbackService = playbackService;
        _audioService = audioService;
        _audioSettingsService = audioSettingsService;
        _settingsService = settingsService;
        _audioNotifier = audioNotifier;
        _discordClient = discordClient;
        _audioModerationLogService = audioModerationLogService;
        _logger = logger;
        _chargeService = chargeService;
    }

    /// <inheritdoc/>
    public async Task<SoundUploadResult> UploadSoundAsync(
        ulong guildId,
        string fileName,
        string soundName,
        Stream fileStream,
        long fileSizeBytes,
        ulong? uploadedById = null,
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
                UploadedAt = DateTime.UtcNow,
                UploadedById = uploadedById
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

        // The price is held once the play is otherwise allowed and committed only when the sound
        // is accepted for playback. Every other way out of this method leaves the hold open, and
        // the finally below drops it, so a refused or failed play never costs anyone anything.
        ChargeHoldResult? charge = null;
        var chargeSettled = false;

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

            // Reserve the price, if this sound has one. An unpriced sound, an exempt user, and a
            // bot with the currency feature switched off all take the free path unchanged.
            charge = await TryHoldPriceAsync(guildId, soundId, userId, cancellationToken);
            if (charge != null && !charge.IsAllowed)
            {
                _logger.LogInformation(
                    "Play of sound {SoundId} in guild {GuildId} by user {UserId} refused: {ChargeStatus}",
                    soundId, guildId, userId, charge.Status);
                BotActivitySource.SetSuccess(activity);
                return new SoundPlayResult
                {
                    Success = false,
                    ErrorMessage = CurrencyFormatting.DescribeChargeRefusal(
                        charge.Status, charge.Price, charge.Balance, charge.CurrencySymbol, "This sound"),
                    ChargeStatus = charge.Status,
                    Price = charge.Price,
                    Balance = charge.Balance,
                    CurrencySymbol = charge.CurrencySymbol
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

            // Resolve the requesting user's display name
            string? requestedByDisplayName = null;
            try
            {
                var guild = _discordClient.GetGuild(guildId);
                var guildUser = guild?.GetUser(userId);
                requestedByDisplayName = guildUser?.DisplayName ?? _discordClient.GetUser(userId)?.Username;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not resolve display name for user {UserId} in guild {GuildId}", userId, guildId);
            }

            // Determine if sound will be queued based on current playback state
            var wasPlaying = _playbackService.IsPlaying(guildId);
            var queueLengthBefore = _playbackService.GetQueueLength(guildId);
            bool willBeQueued = queueEnabled && wasPlaying;
            int? queuePosition = willBeQueued ? queueLengthBefore + 1 : null;

            // Play the sound
            await _playbackService.PlayAsync(guildId, sound, queueEnabled, filter, requestedByDisplayName, cancellationToken);
            _logger.LogInformation("Successfully started playback of sound {SoundName} ({SoundId}) in guild {GuildId}",
                sound.Name, sound.Id, guildId);

            // The sound is accepted for playback, so it is paid for. Someone skipping it partway
            // through does not get anyone their money back; that is the point of charging here.
            var balanceAfter = await CommitPriceAsync(charge, guildId, soundId, cancellationToken);
            chargeSettled = true;

            // Log play event
            await _soundService.LogPlayAsync(sound.Id, guildId, userId, cancellationToken);

            // Log to audio moderation log (fire-and-forget)
            _audioModerationLogService.LogPlayback(guildId, userId, AudioFeatureType.Soundboard, sound.Name, channelId: null);

            BotActivitySource.SetSuccess(activity);

            return new SoundPlayResult
            {
                Success = true,
                Sound = sound,
                WasQueued = willBeQueued,
                QueuePosition = queuePosition,
                ChargeStatus = charge?.Status,
                // A balance means the spend landed. Nothing held, or a commit that got away,
                // leaves all three null so nobody is shown a cost they were not charged.
                Price = balanceAfter.HasValue ? charge!.Price : null,
                Balance = balanceAfter,
                CurrencySymbol = balanceAfter.HasValue ? charge!.CurrencySymbol : null
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
        finally
        {
            // Every path that did not reach playback gets here with the hold still open.
            if (!chargeSettled && _chargeService != null && charge?.HoldId is Guid holdId)
            {
                // Not the caller's token: a cancelled play still has to give the money back.
                await _chargeService.ReleaseAsync(holdId, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Asks the charge seam to reserve this sound's price, or returns null when the currency
    /// feature is switched off — by configuration or by the runtime setting — and there is
    /// nothing to charge against.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="soundId">The sound being played.</param>
    /// <param name="userId">Discord user snowflake ID paying.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task<ChargeHoldResult?> TryHoldPriceAsync(
        ulong guildId,
        Guid soundId,
        ulong userId,
        CancellationToken cancellationToken)
    {
        if (_chargeService == null)
        {
            return null;
        }

        // The same runtime switch the currency commands check. An administrator turning the
        // feature off stops sounds costing anything, without touching any price entry.
        var currencyEnabled = await _settingsService.GetSettingValueAsync<bool?>(
            "Features:CurrencyEnabled", cancellationToken) ?? true;

        if (!currencyEnabled)
        {
            return null;
        }

        // A fresh key per attempt: two plays of the same sound are two charges, while a retried
        // commit of one attempt still writes one row.
        var idempotencyKey = $"sound:{guildId}:{soundId}:{userId}:{Guid.NewGuid()}";

        return await _chargeService.TryHoldAsync(
            userId,
            guildId,
            CurrencyFeatureKeys.Soundboard(soundId),
            idempotencyKey,
            cancellationToken);
    }

    /// <summary>
    /// Turns a hold into the ledger's <c>Spend</c> row and returns the balance left behind, or
    /// null when nothing was held or the commit did not land.
    /// </summary>
    /// <param name="charge">The hold taken before playback, if any.</param>
    /// <param name="guildId">Discord guild snowflake ID, for logging.</param>
    /// <param name="soundId">The sound being played, for logging.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task<long?> CommitPriceAsync(
        ChargeHoldResult? charge,
        ulong guildId,
        Guid soundId,
        CancellationToken cancellationToken)
    {
        if (_chargeService == null || charge?.HoldId is not Guid holdId)
        {
            return null;
        }

        var commit = await _chargeService.CommitAsync(holdId, cancellationToken);
        if (commit.Success)
        {
            return commit.Balance;
        }

        // The sound is already going out over the wire, so this is a charge that got away rather
        // than a play that failed. The charge service has dropped the hold either way.
        _logger.LogWarning(
            "Could not charge for sound {SoundId} in guild {GuildId} after playback started: {Error}",
            soundId, guildId, commit.Error);

        return null;
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
