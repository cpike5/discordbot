using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Handles auto-moderation detection by processing MessageReceived and UserJoined events.
/// </summary>
public class AutoModerationHandler
{
    private readonly ISpamDetectionService _spamService;
    private readonly IContentFilterService _contentFilterService;
    private readonly IRaidDetectionService _raidService;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<AutoModerationHandler> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public AutoModerationHandler(
        ISpamDetectionService spamService,
        IContentFilterService contentFilterService,
        IRaidDetectionService raidService,
        DiscordSocketClient client,
        IServiceScopeFactory scopeFactory,
        ILogger<AutoModerationHandler> logger)
    {
        _spamService = spamService;
        _contentFilterService = contentFilterService;
        _raidService = raidService;
        _client = client;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Handles incoming messages for spam and content detection.
    /// </summary>
    public async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        // Skip bot messages, non-guild messages, and system messages
        if (message.Author.IsBot)
            return;

        var guildChannel = message.Channel as SocketGuildChannel;
        if (guildChannel == null)
            return;

        if (message is not SocketUserMessage)
            return;

        try
        {
            var guildId = guildChannel.Guild.Id;
            var userId = message.Author.Id;
            var channelId = message.Channel.Id;
            var content = message.Content;
            var messageId = message.Id;
            var accountCreated = message.Author.CreatedAt.UtcDateTime;

            _logger.LogDebug(
                "Processing message {MessageId} from user {UserId} in channel {ChannelId} of guild {GuildId}",
                messageId, userId, channelId, guildId);

            // Run spam detection
            var spamResult = await _spamService.AnalyzeMessageAsync(
                guildId, userId, channelId, content, messageId, accountCreated);

            if (spamResult != null)
            {
                using var spamActivity = BotActivitySource.StartEventActivity(
                    TracingConstants.Spans.DiscordEventAutoModSpamDetected,
                    guildId: guildId,
                    channelId: channelId,
                    userId: userId);

                spamActivity?.SetTag(TracingConstants.Attributes.AutoModRuleType, spamResult.RuleType.ToString());
                spamActivity?.SetTag(TracingConstants.Attributes.AutoModSeverity, spamResult.Severity.ToString());
                spamActivity?.SetTag(TracingConstants.Attributes.MessageId, messageId.ToString());

                _logger.LogInformation(
                    "Spam detected in message {MessageId} from user {UserId}: {Description}",
                    messageId, userId, spamResult.Description);

                await HandleDetectionResultAsync(guildId, userId, channelId, spamResult, message);

                BotActivitySource.SetSuccess(spamActivity);
            }

            // Run content filter
            var contentResult = await _contentFilterService.AnalyzeMessageAsync(
                guildId, content, userId, channelId, messageId);

            if (contentResult != null)
            {
                using var contentActivity = BotActivitySource.StartEventActivity(
                    TracingConstants.Spans.DiscordEventAutoModContentFiltered,
                    guildId: guildId,
                    channelId: channelId,
                    userId: userId);

                contentActivity?.SetTag(TracingConstants.Attributes.AutoModRuleType, contentResult.RuleType.ToString());
                contentActivity?.SetTag(TracingConstants.Attributes.AutoModSeverity, contentResult.Severity.ToString());
                contentActivity?.SetTag(TracingConstants.Attributes.MessageId, messageId.ToString());

                _logger.LogInformation(
                    "Prohibited content detected in message {MessageId} from user {UserId}: {Description}",
                    messageId, userId, contentResult.Description);

                await HandleDetectionResultAsync(guildId, userId, channelId, contentResult, message);

                BotActivitySource.SetSuccess(contentActivity);
            }

            // Check if user is on watchlist - log activity
            await CheckWatchlistActivityAsync(guildId, userId, message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in auto-moderation message handler for message {MessageId}", message.Id);
        }
    }

    /// <summary>
    /// Handles user joins for raid detection.
    /// </summary>
    public async Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        try
        {
            var guildId = user.Guild.Id;
            var userId = user.Id;
            var accountCreated = user.CreatedAt.UtcDateTime;

            _logger.LogDebug(
                "Processing user join for {UserId} ({Username}) in guild {GuildId}",
                userId, user.Username, guildId);

            // Run raid detection
            var raidResult = await _raidService.AnalyzeJoinAsync(guildId, userId, accountCreated);

            if (raidResult != null)
            {
                using var raidActivity = BotActivitySource.StartEventActivity(
                    TracingConstants.Spans.DiscordEventAutoModRaidDetected,
                    guildId: guildId,
                    userId: userId);

                var accountAgeDays = (DateTime.UtcNow - accountCreated).Days;

                raidActivity?.SetTag(TracingConstants.Attributes.AutoModRuleType, raidResult.RuleType.ToString());
                raidActivity?.SetTag(TracingConstants.Attributes.AutoModSeverity, raidResult.Severity.ToString());
                raidActivity?.SetTag(TracingConstants.Attributes.MemberAccountAgeDays, accountAgeDays);
                raidActivity?.SetTag(TracingConstants.Attributes.MemberIsBot, user.IsBot);

                _logger.LogWarning(
                    "Raid activity detected for user {UserId} join in guild {GuildId}: {Description}",
                    userId, guildId, raidResult.Description);

                await HandleDetectionResultAsync(guildId, userId, null, raidResult, null, user);

                BotActivitySource.SetSuccess(raidActivity);
            }

            // Check if user is on watchlist - alert moderators
            await CheckWatchlistJoinAsync(guildId, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in auto-moderation user joined handler for user {UserId} in guild {GuildId}",
                user.Id, user.Guild.Id);
        }
    }

    private async Task HandleDetectionResultAsync(
        ulong guildId,
        ulong userId,
        ulong? channelId,
        DetectionResultDto result,
        SocketMessage? message,
        SocketGuildUser? joinedUser = null)
    {
        // Create flagged event
        using var scope = _scopeFactory.CreateScope();
        var flaggedEventService = scope.ServiceProvider.GetRequiredService<IFlaggedEventService>();

        var evidence = System.Text.Json.JsonSerializer.Serialize(result.Evidence);
        var flaggedEvent = await flaggedEventService.CreateEventAsync(
            guildId,
            userId,
            channelId,
            result.RuleType,
            result.Severity,
            result.Description,
            evidence);

        _logger.LogInformation(
            "Auto-mod flagged event created: {EventId} for user {UserId} in guild {GuildId}, type: {RuleType}, severity: {Severity}",
            flaggedEvent.Id, userId, guildId, result.RuleType, result.Severity);

        // One post per event in the mod-log channel: the action taken, or, when nothing was done on
        // its own, the event itself for a moderator to review. Low and Medium events that needed no
        // action stay in the portal's flagged-events list only; they would flood the channel.
        var modLog = scope.ServiceProvider.GetRequiredService<IModLogNotifier>();
        var context = new ModLogFlaggedContext(
            MessageContent: message?.Content,
            AccountCreatedAt: joinedUser?.CreatedAt,
            JoinedAt: joinedUser?.JoinedAt);

        if (result.ShouldAutoAction && result.RecommendedAction != AutoAction.None)
        {
            var succeeded = await ExecuteAutoActionAsync(guildId, userId, result.RecommendedAction, message, joinedUser);
            await modLog.AutoActionAsync(flaggedEvent, result.RecommendedAction, succeeded, context);
        }
        else if (result.Severity >= Severity.High)
        {
            await modLog.FlaggedEventAsync(flaggedEvent, context);
        }
    }

    /// <summary>
    /// Performs the configured automatic action. Returns whether Discord accepted it; a failure is
    /// logged here and reported to the mod-log feed by the caller, never thrown.
    /// </summary>
    private async Task<bool> ExecuteAutoActionAsync(
        ulong guildId,
        ulong userId,
        AutoAction action,
        SocketMessage? message,
        SocketGuildUser? user)
    {
        var succeeded = false;

        using var activity = BotActivitySource.StartEventActivity(
            TracingConstants.Spans.ServiceAutoModExecuteAction,
            guildId: guildId,
            userId: userId);

        try
        {
            activity?.SetTag(TracingConstants.Attributes.AutoModActionType, action.ToString());

            var guild = _client.GetGuild(guildId);
            if (guild == null)
            {
                _logger.LogWarning(
                    "Cannot execute auto-action {Action} for user {UserId}: guild {GuildId} not found",
                    action, userId, guildId);
                activity?.SetTag("guild.found", false);
                BotActivitySource.SetSuccess(activity);
                return false;
            }

            activity?.SetTag("guild.found", true);

            switch (action)
            {
                case AutoAction.Delete:
                    if (message != null)
                    {
                        await message.DeleteAsync();
                        _logger.LogInformation(
                            "Auto-action: Deleted message {MessageId} from user {UserId} in guild {GuildId}",
                            message.Id, userId, guildId);
                        activity?.SetTag("action.success", true);
                    succeeded = true;
                    }
                    else
                    {
                        activity?.SetTag("action.success", false);
                        activity?.SetTag("action.failure_reason", "message_not_available");
                    }
                    break;

                case AutoAction.Warn:
                    // Warning requires creating a moderation case
                    // This requires scoped ModerationCaseService
                    _logger.LogInformation(
                        "Auto-action: Warn action for user {UserId} in guild {GuildId} (case creation not implemented in handler)",
                        userId, guildId);
                    activity?.SetTag("action.success", false);
                    activity?.SetTag("action.failure_reason", "not_implemented");
                    break;

                case AutoAction.Mute:
                    var guildUser = user ?? guild.GetUser(userId);
                    if (guildUser != null)
                    {
                        await guildUser.SetTimeOutAsync(
                            TimeSpan.FromHours(1),
                            new RequestOptions { AuditLogReason = "Auto-moderation: Automatic mute" });

                        _logger.LogInformation(
                            "Auto-action: Muted user {UserId} ({Username}) in guild {GuildId} for 1 hour",
                            userId, guildUser.Username, guildId);
                        activity?.SetTag("action.success", true);
                    succeeded = true;
                        activity?.SetTag("action.timeout_hours", 1);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Cannot mute user {UserId} in guild {GuildId}: user not found",
                            userId, guildId);
                        activity?.SetTag("action.success", false);
                        activity?.SetTag("action.failure_reason", "user_not_found");
                    }
                    break;

                case AutoAction.Kick:
                    var kickUser = user ?? guild.GetUser(userId);
                    if (kickUser != null)
                    {
                        await kickUser.KickAsync("Auto-moderation: Automatic kick");

                        _logger.LogInformation(
                            "Auto-action: Kicked user {UserId} ({Username}) from guild {GuildId}",
                            userId, kickUser.Username, guildId);
                        activity?.SetTag("action.success", true);
                    succeeded = true;
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Cannot kick user {UserId} from guild {GuildId}: user not found",
                            userId, guildId);
                        activity?.SetTag("action.success", false);
                        activity?.SetTag("action.failure_reason", "user_not_found");
                    }
                    break;

                case AutoAction.Ban:
                    await guild.AddBanAsync(userId, 0, "Auto-moderation: Automatic ban");

                    _logger.LogInformation(
                        "Auto-action: Banned user {UserId} from guild {GuildId}",
                        userId, guildId);
                    activity?.SetTag("action.success", true);
                    succeeded = true;
                    break;

                default:
                    _logger.LogDebug(
                        "Auto-action {Action} for user {UserId} in guild {GuildId} - no action taken",
                        action, userId, guildId);
                    activity?.SetTag("action.success", false);
                    activity?.SetTag("action.failure_reason", "no_action_configured");
                    break;
            }

            BotActivitySource.SetSuccess(activity);
            return succeeded;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to execute auto-action {Action} for user {UserId} in guild {GuildId}",
                action, userId, guildId);
            BotActivitySource.RecordException(activity, ex);
            return false;
        }
    }

    private async Task CheckWatchlistActivityAsync(ulong guildId, ulong userId, SocketMessage message)
    {
        using var scope = _scopeFactory.CreateScope();
        var watchlistService = scope.ServiceProvider.GetRequiredService<IWatchlistService>();

        if (await watchlistService.IsOnWatchlistAsync(guildId, userId))
        {
            _logger.LogDebug(
                "Watchlist user {UserId} activity detected in guild {GuildId}: message in channel {ChannelId}",
                userId, guildId, message.Channel.Id);
        }
    }

    private async Task CheckWatchlistJoinAsync(ulong guildId, SocketGuildUser user)
    {
        using var scope = _scopeFactory.CreateScope();
        var watchlistService = scope.ServiceProvider.GetRequiredService<IWatchlistService>();

        var entry = await watchlistService.GetEntryAsync(guildId, user.Id);
        if (entry != null)
        {
            _logger.LogWarning(
                "Watchlist user {UserId} ({Username}) rejoined guild {GuildId}. Reason on watchlist: {Reason}",
                user.Id, user.Username, guildId, entry.Reason ?? "(no reason specified)");

            // Alert mod channel about watchlist user rejoining
            await SendWatchlistRejoinAlertAsync(guildId, user, entry);
        }
    }

    private async Task SendWatchlistRejoinAlertAsync(ulong guildId, SocketGuildUser user, WatchlistEntryDto entry)
    {
        try
        {
            var guild = _client.GetGuild(guildId);
            if (guild == null)
                return;

            var modChannel = guild.TextChannels
                .FirstOrDefault(c => c.Name.Contains("mod-log", StringComparison.OrdinalIgnoreCase) ||
                                    c.Name.Contains("mod-alert", StringComparison.OrdinalIgnoreCase));

            if (modChannel == null)
            {
                _logger.LogDebug(
                    "No mod channel found for watchlist rejoin alert in guild {GuildId}",
                    guildId);
                return;
            }

            var embed = new EmbedBuilder()
                .WithTitle("👁️ Watchlist User Rejoined")
                .WithColor(Color.Orange)
                .AddField("User", $"<@{user.Id}> ({user.Id})", true)
                .AddField("Account Age", $"<t:{user.CreatedAt.ToUnixTimeSeconds()}:R>", true)
                .AddField("Added by", $"<@{entry.AddedByUserId}>", true)
                .AddField("Added", $"<t:{new DateTimeOffset(entry.AddedAt).ToUnixTimeSeconds()}:R>", true)
                .WithTimestamp(DateTimeOffset.UtcNow);

            if (!string.IsNullOrEmpty(entry.Reason))
            {
                embed.AddField("Watch Reason", entry.Reason);
            }

            await modChannel.SendMessageAsync(embed: embed.Build());

            _logger.LogInformation(
                "Sent watchlist rejoin alert for user {UserId} in guild {GuildId}",
                user.Id, guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send watchlist rejoin alert for user {UserId} in guild {GuildId}",
                user.Id, guildId);
        }
    }
}
