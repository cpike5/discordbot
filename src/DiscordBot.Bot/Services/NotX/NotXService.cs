using Discord;
using Discord.WebSocket;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.NotX;

/// <summary>
/// Orchestrates the not-X tweet preview pipeline: extracts tweet metadata via fxtwitter,
/// applies guild-level gating rules, builds Discord embeds, and posts them to the
/// appropriate channel.
/// </summary>
public class NotXService : INotXService
{
    private readonly INotXGuildSettingsRepository _repository;
    private readonly IFxTwitterClient _fxTwitterClient;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<NotXService> _logger;

    public NotXService(
        INotXGuildSettingsRepository repository,
        IFxTwitterClient fxTwitterClient,
        DiscordSocketClient client,
        ILogger<NotXService> logger)
    {
        _repository = repository;
        _fxTwitterClient = fxTwitterClient;
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ProcessTweetAsync(
        ulong guildId,
        ulong channelId,
        ulong sourceMessageId,
        string tweetUrl,
        bool ignoreSettingsGate = false)
    {
        // Step 1 — Extract screen name and tweet ID from URL
        var matches = TweetUrlExtractor.Extract(tweetUrl);
        if (matches.Count == 0)
        {
            _logger.LogDebug(
                "ProcessTweetAsync: no tweet URL recognised in '{TweetUrl}' for guild {GuildId}",
                tweetUrl, guildId);
            return false;
        }

        var match = matches[0];
        var screenName = match.ScreenName;
        var tweetId = match.TweetId;

        // Step 2 — Gate on guild settings (skipped for manual context-menu invocations)
        NotXGuildSettings? settings = null;
        if (!ignoreSettingsGate)
        {
            settings = await _repository.GetByGuildIdAsync(guildId);

            if (settings is null || !settings.IsEnabled)
            {
                _logger.LogDebug(
                    "not-X is disabled for guild {GuildId}; skipping tweet {TweetId}",
                    guildId, tweetId);
                return false;
            }

            var monitoredChannels = settings.GetMonitoredChannelIds();
            if (monitoredChannels.Count > 0 && !monitoredChannels.Contains(channelId))
            {
                _logger.LogDebug(
                    "Channel {ChannelId} is not in the monitored list for guild {GuildId}; skipping tweet {TweetId}",
                    channelId, guildId, tweetId);
                return false;
            }
        }
        else
        {
            // Still load settings so output-channel routing is respected
            settings = await _repository.GetByGuildIdAsync(guildId);
        }

        // Step 3 — Fetch tweet data
        var tweet = await _fxTwitterClient.FetchTweetAsync(screenName, tweetId);
        if (tweet is null)
        {
            _logger.LogDebug(
                "fxtwitter returned no result for tweet {TweetId} in guild {GuildId}",
                tweetId, guildId);
            return false;
        }

        // Step 4 — Sensitivity gate (skipped for manual invocations)
        if (!ignoreSettingsGate && settings is not null && settings.SensitiveOnly && !tweet.PossiblySensitive)
        {
            _logger.LogDebug(
                "Tweet {TweetId} is not marked sensitive and SensitiveOnly is enabled for guild {GuildId}; skipping",
                tweetId, guildId);
            return false;
        }

        // Step 5 — Build embeds
        var isSensitive = tweet.PossiblySensitive && !(settings?.HideSensitiveLabel ?? false);

        // Determine output channel and whether this is a cross-channel post
        var targetChannelId = settings?.OutputChannelId ?? channelId;
        var isCrossChannel = targetChannelId != channelId;

        ulong? crossChannelSourceMessageId = isCrossChannel ? sourceMessageId : null;
        ulong? crossChannelSourceChannelId = isCrossChannel ? channelId : null;

        var embeds = NotXEmbedBuilder.Build(
            tweet,
            isSensitive,
            crossChannelSourceMessageId,
            crossChannelSourceChannelId,
            guildId);

        // Step 6 — Resolve target channel
        var targetChannel = _client.GetChannel(targetChannelId) as IMessageChannel;
        if (targetChannel is null)
        {
            _logger.LogWarning(
                "Cannot resolve output channel {ChannelId} for guild {GuildId}; cannot post tweet {TweetId}",
                targetChannelId, guildId, tweetId);
            return false;
        }

        // Step 7 — Post embeds
        MessageReference? messageReference = isCrossChannel
            ? null
            : new MessageReference(sourceMessageId);

        await targetChannel.SendMessageAsync(
            embeds: embeds,
            messageReference: messageReference);

        _logger.LogInformation(
            "Posted tweet embed for {TweetId} in guild {GuildId} to channel {OutputChannelId} (sensitive={PossiblySensitive})",
            tweetId, guildId, targetChannelId, tweet.PossiblySensitive);

        return true;
    }

    /// <inheritdoc />
    public Task<NotXGuildSettings> GetOrCreateSettingsAsync(ulong guildId)
        => _repository.GetOrCreateAsync(guildId);

    /// <inheritdoc />
    public async Task UpdateSettingsAsync(NotXGuildSettings settings)
    {
        settings.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(settings);
    }
}
