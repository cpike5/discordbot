using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Tts;

/// <summary>
/// Service implementation for managing guild TTS settings.
/// Handles configuration retrieval and updates for TTS features.
/// </summary>
public class TtsSettingsService : ITtsSettingsService
{
    private readonly IGuildTtsSettingsRepository _settingsRepository;
    private readonly ITtsMessageRepository _messageRepository;
    private readonly ILogger<TtsSettingsService> _logger;

    public TtsSettingsService(
        IGuildTtsSettingsRepository settingsRepository,
        ITtsMessageRepository messageRepository,
        ILogger<TtsSettingsService> logger)
    {
        _settingsRepository = settingsRepository;
        _messageRepository = messageRepository;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<GuildTtsSettings> GetOrCreateSettingsAsync(ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "tts_settings",
            "get_or_create",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Getting TTS settings for guild {GuildId}", guildId);

            var settings = await _settingsRepository.GetOrCreateAsync(guildId, ct);

            _logger.LogDebug(
                "Retrieved TTS settings for guild {GuildId}: TtsEnabled={TtsEnabled}, MaxMessageLength={MaxMessageLength}",
                guildId, settings.TtsEnabled, settings.MaxMessageLength);

            BotActivitySource.SetSuccess(activity);
            return settings;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task UpdateSettingsAsync(GuildTtsSettings settings, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "tts_settings",
            "update",
            guildId: settings.GuildId);

        try
        {
            _logger.LogInformation("Updating TTS settings for guild {GuildId}", settings.GuildId);

            settings.UpdatedAt = DateTime.UtcNow;
            await _settingsRepository.UpdateAsync(settings, ct);

            _logger.LogInformation("TTS settings updated for guild {GuildId}", settings.GuildId);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsTtsEnabledAsync(ulong guildId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "tts_settings",
            "is_enabled",
            guildId: guildId);

        try
        {
            _logger.LogDebug("Checking if TTS is enabled for guild {GuildId}", guildId);

            var settings = await _settingsRepository.GetByGuildIdAsync(guildId, ct);

            // If no settings exist, TTS is enabled by default
            var isEnabled = settings?.TtsEnabled ?? true;

            _logger.LogDebug("TTS enabled for guild {GuildId}: {IsEnabled}", guildId, isEnabled);

            BotActivitySource.SetSuccess(activity);
            return isEnabled;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsUserRateLimitedAsync(ulong guildId, ulong userId, CancellationToken ct = default)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "tts_settings",
            "check_rate_limit",
            guildId: guildId,
            userId: userId);

        try
        {
            _logger.LogDebug(
                "Checking rate limit for user {UserId} in guild {GuildId}",
                userId, guildId);

            var settings = await _settingsRepository.GetOrCreateAsync(guildId, ct);

            // Get message count in the last minute
            var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            var messageCount = await _messageRepository.GetUserMessageCountAsync(
                guildId, userId, oneMinuteAgo, ct);

            var isRateLimited = messageCount >= settings.RateLimitPerMinute;

            _logger.LogDebug(
                "User {UserId} in guild {GuildId}: {MessageCount}/{RateLimit} messages in last minute, rate limited: {IsRateLimited}",
                userId, guildId, messageCount, settings.RateLimitPerMinute, isRateLimited);

            BotActivitySource.SetSuccess(activity);
            return isRateLimited;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }
}
