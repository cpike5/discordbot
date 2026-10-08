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
    public Task<ulong?> CaseCreatedAsync(ModerationCaseDto moderationCase, CancellationToken ct = default)
        => PostAsync(
            moderationCase.GuildId,
            ModLogEventKinds.Cases,
            $"case #{moderationCase.CaseNumber}",
            () => ModLogEmbeds.ForCase(moderationCase, _client.CurrentUser?.Id ?? 0),
            () => ModLogEmbeds.CaseComponents(moderationCase, _application.BaseUrl),
            ct);

    /// <inheritdoc />
    public Task<ulong?> FlaggedEventAsync(FlaggedEventDto flaggedEvent, ModLogFlaggedContext context, CancellationToken ct = default)
        => PostAsync(
            flaggedEvent.GuildId,
            ModLogEventKinds.FlaggedEvents,
            $"flagged event {flaggedEvent.Id}",
            () => ModLogEmbeds.ForFlaggedEvent(flaggedEvent, context),
            () => ModLogEmbeds.FlaggedEventComponents(flaggedEvent.Id),
            ct);

    /// <inheritdoc />
    public Task<ulong?> AutoActionAsync(FlaggedEventDto flaggedEvent, AutoAction action, bool succeeded, ModLogFlaggedContext context, CancellationToken ct = default)
        => PostAsync(
            flaggedEvent.GuildId,
            ModLogEventKinds.AutoActions,
            $"auto-action {action} for event {flaggedEvent.Id}",
            () => ModLogEmbeds.ForAutoAction(flaggedEvent, action, succeeded, context),
            () => ModLogEmbeds.FlaggedEventComponents(flaggedEvent.Id),
            ct);

    /// <summary>
    /// The one delivery path: resolve the channel for this kind, build, send, and turn every failure
    /// into a log line. <paramref name="what"/> names the item in those lines.
    /// </summary>
    private async Task<ulong?> PostAsync(
        ulong guildId,
        ModLogEventKinds kind,
        string what,
        Func<Embed> embed,
        Func<MessageComponent> components,
        CancellationToken ct)
    {
        try
        {
            var channel = await ResolveChannelAsync(guildId, kind, ct);
            if (channel is null)
            {
                return null;
            }

            var message = await channel.SendMessageAsync(embed: embed(), components: components());

            _logger.LogDebug("Posted {What} to mod-log channel {ChannelId} in guild {GuildId}", what, channel.Id, guildId);

            return message.Id;
        }
        catch (HttpException ex) when (ex.HttpCode == System.Net.HttpStatusCode.Forbidden)
        {
            WarnOnce(guildId,
                "The bot may not post in the mod-log channel of guild {GuildId}: {Reason}. Give it Send Messages and Embed Links there, or choose another channel.",
                guildId, ex.Reason ?? ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to post {What} to the mod-log channel of guild {GuildId}", what, guildId);
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
                channelId, guildId);
            return null;
        }

        return channel;
    }

    private void WarnOnce(ulong guildId, string messageTemplate, params object[] args)
    {
        var key = WarnedCacheKeyPrefix + guildId;
        if (_cache.TryGetValue(key, out _))
        {
            return;
        }

        _cache.Set(key, true, new MemoryCacheEntryOptions().SetAbsoluteExpiration(WarnInterval).SetSize(1));
#pragma warning disable CA2254 // The template is one of two constants above
        _logger.LogWarning(messageTemplate, args);
#pragma warning restore CA2254
    }
}
