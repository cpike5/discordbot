using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Services.NotX;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Context menu command module for the not-X feature.
/// Must be a separate class from <see cref="NotXCommandModule"/> because context menu commands
/// cannot be registered inside a <see cref="GroupAttribute"/>-decorated module.
/// </summary>
[RequireContext(ContextType.Guild)]
public class NotXContextMenuModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly INotXService _notXService;
    private readonly ILogger<NotXContextMenuModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotXContextMenuModule"/> class.
    /// </summary>
    public NotXContextMenuModule(
        INotXService notXService,
        ILogger<NotXContextMenuModule> logger)
    {
        _notXService = notXService;
        _logger = logger;
    }

    /// <summary>
    /// Manually fetches tweet previews for any X/Twitter links in the target message.
    /// Bypasses the IsEnabled and SensitiveOnly guild settings so the command works
    /// even when auto-posting is disabled or the tweet is not flagged sensitive.
    /// Guild output channel routing is still respected.
    /// </summary>
    [MessageCommand("Fetch Tweet")]
    public async Task FetchTweetAsync(IMessage message)
    {
        await DeferAsync(ephemeral: true);

        _logger.LogInformation(
            "Fetch Tweet context menu used by {Username} (ID: {UserId}) on message {MessageId} in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            message.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        var matches = TweetUrlExtractor.Extract(message.Content);

        if (matches.Count == 0)
        {
            _logger.LogDebug(
                "Fetch Tweet: no tweet URLs found in message {MessageId} (guild {GuildId})",
                message.Id,
                Context.Guild.Id);

            await FollowupAsync(
                embed: EmbedHelper.Info("No Tweet Found", "This message doesn't contain any X/Twitter links."),
                ephemeral: true);
            return;
        }

        var guildId = Context.Guild.Id;
        var channelId = Context.Channel.Id;
        var successCount = 0;
        var results = new List<string>(matches.Count);

        foreach (var match in matches)
        {
            _logger.LogDebug(
                "Fetch Tweet: processing URL {TweetUrl} from message {MessageId} in guild {GuildId}",
                match.FullUrl,
                message.Id,
                guildId);

            var posted = await _notXService.ProcessTweetAsync(
                guildId,
                channelId,
                sourceMessageId: message.Id,
                tweetUrl: match.FullUrl,
                ignoreSettingsGate: true);

            if (posted)
            {
                successCount++;
                results.Add($"✅ Posted preview for <{match.FullUrl}>");
            }
            else
            {
                results.Add($"⚠️ Could not fetch <{match.FullUrl}>");
            }
        }

        var resultText = string.Join("\n", results);
        var anySucceeded = successCount > 0;

        if (anySucceeded)
        {
            _logger.LogInformation(
                "Fetch Tweet: successfully posted {Count} of {Total} tweet previews for message {MessageId} in guild {GuildId}",
                successCount,
                matches.Count,
                message.Id,
                guildId);

            await FollowupAsync(
                embed: EmbedHelper.Success("Tweet Fetched", resultText),
                ephemeral: true);
        }
        else
        {
            _logger.LogWarning(
                "Fetch Tweet: could not fetch any of {Count} tweet URL(s) from message {MessageId} in guild {GuildId}",
                matches.Count,
                message.Id,
                guildId);

            await FollowupAsync(
                embed: EmbedHelper.Info("Could Not Fetch", resultText),
                ephemeral: true);
        }
    }
}
