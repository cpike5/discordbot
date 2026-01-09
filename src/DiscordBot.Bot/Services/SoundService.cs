using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service implementation for managing soundboard sounds.
/// Handles sound CRUD operations, validation, and usage tracking.
/// </summary>
public class SoundService : ISoundService
{
    private readonly ISoundRepository _soundRepository;
    private readonly IGuildAudioSettingsRepository _settingsRepository;
    private readonly ILogger<SoundService> _logger;
    private readonly SoundboardOptions _options;

    public SoundService(
        ISoundRepository soundRepository,
        IGuildAudioSettingsRepository settingsRepository,
        ILogger<SoundService> logger,
        IOptions<SoundboardOptions> options)
    {
        _soundRepository = soundRepository;
        _settingsRepository = settingsRepository;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<Sound?> GetByIdAsync(Guid id, ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound",
            "get_by_id",
            guildId: guildId,
            entityId: id.ToString());

        try
        {
            _logger.LogDebug("Getting sound {SoundId} for guild {GuildId}", id, guildId);

            var sound = await _soundRepository.GetByIdAndGuildAsync(id, guildId, ct);

            if (sound == null)
            {
                _logger.LogDebug("Sound {SoundId} not found or does not belong to guild {GuildId}", id, guildId);
            }

            BotActivitySource.SetSuccess(activity);
            return sound;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Sound>> GetAllByGuildAsync(ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound",
            "get_all_by_guild",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Getting all sounds for guild {GuildId}", guildId);

            var sounds = await _soundRepository.GetByGuildIdAsync(guildId, ct);

            _logger.LogInformation("Retrieved {Count} sounds for guild {GuildId}", sounds.Count, guildId);

            BotActivitySource.SetRecordsReturned(activity, sounds.Count);
            BotActivitySource.SetSuccess(activity);
            return sounds;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Sound?> GetByNameAsync(string name, ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound",
            "get_by_name",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Getting sound by name '{Name}' for guild {GuildId}", name, guildId);

            var sound = await _soundRepository.GetByNameAndGuildAsync(name, guildId, ct);

            if (sound == null)
            {
                _logger.LogDebug("Sound '{Name}' not found in guild {GuildId}", name, guildId);
            }

            BotActivitySource.SetSuccess(activity);
            return sound;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<Sound> CreateSoundAsync(Sound sound, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound",
            "create",
            guildId: sound.GuildId,
            entityId: sound.Id.ToString());

        try
        {
            _logger.LogInformation("Creating sound '{Name}' for guild {GuildId}", sound.Name, sound.GuildId);

            // Check if a sound with the same name already exists
            var existing = await _soundRepository.GetByNameAndGuildAsync(sound.Name, sound.GuildId, ct);
            if (existing != null)
            {
                _logger.LogWarning("Cannot create sound '{Name}' for guild {GuildId}: name already exists",
                    sound.Name, sound.GuildId);
                throw new InvalidOperationException($"A sound with the name '{sound.Name}' already exists in this guild.");
            }

            // Set default values if not already set
            if (sound.UploadedAt == default)
            {
                sound.UploadedAt = DateTime.UtcNow;
            }

            if (sound.PlayCount == 0)
            {
                sound.PlayCount = 0; // Explicitly ensure it's 0
            }

            await _soundRepository.AddAsync(sound, ct);

            _logger.LogInformation("Sound '{Name}' ({SoundId}) created successfully for guild {GuildId}",
                sound.Name, sound.Id, sound.GuildId);

            BotActivitySource.SetSuccess(activity);
            return sound;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteSoundAsync(Guid id, ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound",
            "delete",
            guildId: guildId,
            entityId: id.ToString());

        try
        {
            _logger.LogInformation("Deleting sound {SoundId} from guild {GuildId}", id, guildId);

            var sound = await _soundRepository.GetByIdAndGuildAsync(id, guildId, ct);
            if (sound == null)
            {
                _logger.LogWarning("Cannot delete sound {SoundId}: not found or does not belong to guild {GuildId}",
                    id, guildId);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            await _soundRepository.DeleteAsync(sound, ct);

            _logger.LogInformation("Sound {SoundId} ('{Name}') deleted from guild {GuildId}",
                id, sound.Name, guildId);

            BotActivitySource.SetSuccess(activity);
            return true;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task IncrementPlayCountAsync(Guid soundId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound",
            "increment_play_count",
            entityId: soundId.ToString());

        try
        {
            _logger.LogDebug("Incrementing play count for sound {SoundId}", soundId);

            await _soundRepository.IncrementPlayCountAsync(soundId, ct);

            _logger.LogDebug("Play count incremented for sound {SoundId}", soundId);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateStorageLimitAsync(ulong guildId, long additionalBytes, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound",
            "validate_storage_limit",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Validating storage limit for guild {GuildId}: {AdditionalBytes} additional bytes",
                guildId, additionalBytes);

            var settings = await _settingsRepository.GetOrCreateAsync(guildId, ct);
            var currentUsage = await _soundRepository.GetTotalStorageUsedAsync(guildId, ct);
            var potentialUsage = currentUsage + additionalBytes;

            var isValid = potentialUsage <= settings.MaxStorageBytes;

            _logger.LogInformation(
                "Storage validation for guild {GuildId}: current={CurrentBytes}, additional={AdditionalBytes}, potential={PotentialBytes}, limit={LimitBytes}, valid={IsValid}",
                guildId, currentUsage, additionalBytes, potentialUsage, settings.MaxStorageBytes, isValid);

            BotActivitySource.SetSuccess(activity);
            return isValid;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ValidateSoundCountLimitAsync(ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound",
            "validate_sound_count_limit",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Validating sound count limit for guild {GuildId}", guildId);

            var settings = await _settingsRepository.GetOrCreateAsync(guildId, ct);
            var currentCount = await _soundRepository.GetSoundCountAsync(guildId, ct);

            var isValid = currentCount < settings.MaxSoundsPerGuild;

            _logger.LogInformation(
                "Sound count validation for guild {GuildId}: current={CurrentCount}, limit={Limit}, valid={IsValid}",
                guildId, currentCount, settings.MaxSoundsPerGuild, isValid);

            BotActivitySource.SetSuccess(activity);
            return isValid;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<long> GetStorageUsedAsync(ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound",
            "get_storage_used",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Getting total storage used for guild {GuildId}", guildId);

            var storageUsed = await _soundRepository.GetTotalStorageUsedAsync(guildId, ct);

            _logger.LogDebug("Guild {GuildId} storage used: {StorageBytes} bytes", guildId, storageUsed);

            BotActivitySource.SetSuccess(activity);
            return storageUsed;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<int> GetSoundCountAsync(ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "sound",
            "get_sound_count",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Getting sound count for guild {GuildId}", guildId);

            var count = await _soundRepository.GetSoundCountAsync(guildId, ct);

            _logger.LogDebug("Guild {GuildId} has {Count} sounds", guildId, count);

            BotActivitySource.SetSuccess(activity);
            return count;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }
}
