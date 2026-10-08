using Discord;
using Discord.Net;
using Discord.WebSocket;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.Moderation;

/// <summary>
/// <see cref="IModLogNotifier"/> over the live Discord client: reads the guild's mod-log settings,
/// resolves the channel, and posts the embed <see cref="ModLogEmbeds"/> builds.
/// </summary>
/// <remarks>
/// Every failure is logged and swallowed. A channel the bot cannot see or post to is reported at
/// Warning once per guild per hour (an <see cref="IMemoryCache"/> key), not on every case, because a
/// busy server would otherwise fill the log with the same line.
/// </remarks>
public class ModLogNotifier : IModLogNotifier
{
    /// <summary>Cache key prefix for the once-per-hour warning about an unreachable channel.</summary>
    public const string WarnedCacheKeyPrefix = "modlog:warned:";

    private static readonly TimeSpan WarnInterval = TimeSpan.FromHours(1);

    private readonly DiscordSocketClient _client;
    private readonly IGuildModerationConfigService _configService;
    private readonly IMemoryCache _cache;
    private readonly ApplicationOptions _application;
    private readonly ILogger<ModLogNotifier> _logger;

    public ModLogNotifier(
        DiscordSocketClient client,
        IGuildModerationConfigService configService,
        IMemoryCache cache,
        IOptions<ApplicationOptions> application,
        ILogger<ModLogNotifier> logger)
    {
        _client = client;
        _configService = configService;
        _cache = cache;
        _application = application.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ulong?> CaseCreatedAsync(ModerationCaseDto moderationCase, CancellationToken ct = default)
    {
        try
        {
            var channel = await ResolveChannelAsync(moderationCase.GuildId, ModLogEventKinds.Cases, ct);
            if (channel is null)
            {
                return null;
            }

            var botUserId = _client.CurrentUser?.Id ?? 0;
            var message = await channel.SendMessageAsync(
                embed: ModLogEmbeds.ForCase(moderationCase, botUserId),
                components: ModLogEmbeds.CaseComponents(moderationCase, _application.BaseUrl));

            _logger.LogDebug("Posted case #{CaseNumber} to mod-log channel {ChannelId} in guild {GuildId}",
                moderationCase.CaseNumber, channel.Id, moderationCase.GuildId);

            return message.Id;
        }
        catch (HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
        {
            WarnOnce(moderationCase.GuildId,
                "The bot may not post in the mod-log channel of guild {GuildId}: {Reason}. Give it Send Messages and Embed Links there, or choose another channel.",
                ex.Reason ?? ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post case #{CaseNumber} to the mod-log channel of guild {GuildId}",
                moderationCase.CaseNumber, moderationCase.GuildId);
            return null;
        }
    }

    /// <summary>
    /// The channel to post to, or null when the feed is off for this kind, the bot cannot see the
    /// guild, or the channel is gone.
    /// </summary>
    private async Task<ITextChannel?> ResolveChannelAsync(ulong guildId, ModLogEventKinds kind, CancellationToken ct)
    {
        var config = await _configService.GetConfigAsync(guildId, ct);
        if (config.ModLogChannelId is not { } channelId || !config.ModLogEvents.HasFlag(kind))
        {
            return null;
        }

        var guild = _client.GetGuild(guildId);
        if (guild is null)
        {
            // Not an error on the guild's side: the bot is offline or not in the guild any more.
            _logger.LogDebug("Guild {GuildId} is not available to the client; skipping the mod-log post", guildId);
            return null;
        }

        var channel = guild.GetTextChannel(channelId);
        if (channel is null)
        {
            WarnOnce(guildId,
                "The mod-log channel {ChannelId} of guild {GuildId} no longer exists or is not visible to the bot. Choose another channel in Moderation Settings.",
                channelId);
            return null;
        }

        return channel;
    }

    private void WarnOnce(ulong guildId, string messageTemplate, object detail)
    {
        var key = WarnedCacheKeyPrefix + guildId;
        if (_cache.TryGetValue(key, out _))
        {
            return;
        }

        _cache.Set(key, true, new MemoryCacheEntryOptions().SetAbsoluteExpiration(WarnInterval).SetSize(1));
#pragma warning disable CA2254 // The template is one of two constants above
        _logger.LogWarning(messageTemplate, detail, guildId);
#pragma warning restore CA2254
    }
}
