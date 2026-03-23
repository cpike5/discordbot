using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for logging audio playback events for moderation purposes.
/// Uses <see cref="IBackgroundTaskRunner"/> to perform fire-and-forget database writes
/// so that playback responses are never blocked by logging.
/// </summary>
public class AudioModerationLogService : IAudioModerationLogService
{
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AudioModerationLogService> _logger;

    private const int MaxContentNameLength = 200;

    public AudioModerationLogService(
        IBackgroundTaskRunner backgroundTaskRunner,
        IServiceScopeFactory scopeFactory,
        ILogger<AudioModerationLogService> logger)
    {
        _backgroundTaskRunner = backgroundTaskRunner;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void LogPlayback(ulong guildId, ulong userId, AudioFeatureType featureType, string contentName, ulong? channelId)
    {
        // Truncate content name to max length
        var truncatedContent = contentName.Length > MaxContentNameLength
            ? contentName[..MaxContentNameLength]
            : contentName;

        var log = new AudioPlaybackLog
        {
            GuildId = guildId,
            UserId = userId,
            FeatureType = featureType,
            ContentName = truncatedContent,
            ChannelId = channelId,
            PlayedAt = DateTime.UtcNow
        };

        _backgroundTaskRunner.Run(async ct =>
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAudioPlaybackLogRepository>();
            await repository.AddAsync(log, ct);

            _logger.LogDebug(
                "Audio playback logged - Guild: {GuildId}, User: {UserId}, Feature: {FeatureType}, Content: {ContentName}",
                guildId, userId, featureType, truncatedContent);
        }, "AudioModerationLog.LogPlayback");
    }
}
