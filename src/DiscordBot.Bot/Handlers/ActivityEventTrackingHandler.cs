using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Handles Discord events and logs anonymous activity events for consent-free analytics.
/// Tracks metadata only (no content) for aggregate analytics and engagement metrics.
/// </summary>
public class ActivityEventTrackingHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActivityEventTrackingHandler> _logger;

    public ActivityEventTrackingHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<ActivityEventTrackingHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Handles the MessageReceived event from DiscordSocketClient.
    /// Logs message activity metadata (no content).
    /// </summary>
    public async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        using var activity = BotActivitySource.StartEventActivity(
            TracingConstants.Spans.DiscordEventMessageReceived,
            guildId: (message.Channel as SocketGuildChannel)?.Guild.Id,
            channelId: message.Channel.Id,
            userId: message.Author.Id);

        try
        {
            // Filter out bot messages and system messages
            if (message.Author.IsBot || message.Author.IsWebhook)
            {
                _logger.LogTrace("Skipping bot/webhook message {MessageId}", message.Id);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            // Only process messages in guild channels
            if (message.Channel is not SocketGuildChannel guildChannel)
            {
                _logger.LogTrace("Skipping non-guild message {MessageId}", message.Id);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            // Check if activity event tracking is enabled
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var isEnabled = await settingsService.GetSettingValueAsync<bool>("Features:ActivityEventTrackingEnabled");

            if (!isEnabled)
            {
                _logger.LogTrace("Activity event tracking is disabled, skipping message {MessageId}", message.Id);
                activity?.SetTag("activity_tracking.enabled", false);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            _logger.LogDebug("Logging activity event: Message from user {UserId} in guild {GuildId}",
                message.Author.Id, guildChannel.Guild.Id);

            var repository = scope.ServiceProvider.GetRequiredService<IUserActivityEventRepository>();

            var activityEvent = new UserActivityEvent
            {
                UserId = message.Author.Id,
                GuildId = guildChannel.Guild.Id,
                ChannelId = message.Channel.Id,
                EventType = ActivityEventType.Message,
                Timestamp = message.Timestamp.UtcDateTime,
                LoggedAt = DateTime.UtcNow
            };

            await repository.AddAsync(activityEvent);

            _logger.LogDebug("Successfully logged Message activity event for user {UserId} in guild {GuildId}",
                message.Author.Id, guildChannel.Guild.Id);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log Message activity event for message {MessageId}", message.Id);
            BotActivitySource.RecordException(activity, ex);
        }
    }

    /// <summary>
    /// Handles the ReactionAdded event from DiscordSocketClient.
    /// Logs reaction activity metadata.
    /// </summary>
    public async Task HandleReactionAddedAsync(
        Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        using var activity = BotActivitySource.StartEventActivity(
            "discord.event.reaction_added",
            guildId: (reaction.Channel as SocketGuildChannel)?.Guild.Id,
            channelId: reaction.Channel.Id,
            userId: reaction.UserId);

        try
        {
            // Filter out bot reactions
            if (reaction.User.IsSpecified && reaction.User.Value.IsBot)
            {
                _logger.LogTrace("Skipping bot reaction from user {UserId}", reaction.UserId);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            // Only process reactions in guild channels
            if (reaction.Channel is not SocketGuildChannel guildChannel)
            {
                _logger.LogTrace("Skipping non-guild reaction from user {UserId}", reaction.UserId);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            // Check if activity event tracking is enabled
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var isEnabled = await settingsService.GetSettingValueAsync<bool>("Features:ActivityEventTrackingEnabled");

            if (!isEnabled)
            {
                _logger.LogTrace("Activity event tracking is disabled, skipping reaction from user {UserId}", reaction.UserId);
                activity?.SetTag("activity_tracking.enabled", false);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            _logger.LogDebug("Logging activity event: Reaction from user {UserId} in guild {GuildId}",
                reaction.UserId, guildChannel.Guild.Id);

            var repository = scope.ServiceProvider.GetRequiredService<IUserActivityEventRepository>();

            var activityEvent = new UserActivityEvent
            {
                UserId = reaction.UserId,
                GuildId = guildChannel.Guild.Id,
                ChannelId = reaction.Channel.Id,
                EventType = ActivityEventType.Reaction,
                Timestamp = DateTime.UtcNow,
                LoggedAt = DateTime.UtcNow
            };

            await repository.AddAsync(activityEvent);

            _logger.LogDebug("Successfully logged Reaction activity event for user {UserId} in guild {GuildId}",
                reaction.UserId, guildChannel.Guild.Id);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log Reaction activity event for user {UserId}", reaction.UserId);
            BotActivitySource.RecordException(activity, ex);
        }
    }

    /// <summary>
    /// Handles the UserVoiceStateUpdated event from DiscordSocketClient.
    /// Logs voice channel join/leave activity metadata.
    /// </summary>
    public async Task HandleUserVoiceStateUpdatedAsync(
        SocketUser user,
        SocketVoiceState before,
        SocketVoiceState after)
    {
        using var activity = BotActivitySource.StartEventActivity(
            "discord.event.user_voice_state_updated",
            guildId: (after.VoiceChannel as SocketGuildChannel)?.Guild.Id ?? (before.VoiceChannel as SocketGuildChannel)?.Guild.Id,
            channelId: after.VoiceChannel?.Id ?? before.VoiceChannel?.Id,
            userId: user.Id);

        try
        {
            // Filter out bot voice state changes
            if (user.IsBot || user.IsWebhook)
            {
                _logger.LogTrace("Skipping bot voice state change for user {UserId}", user.Id);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            // Check if activity event tracking is enabled
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var isEnabled = await settingsService.GetSettingValueAsync<bool>("Features:ActivityEventTrackingEnabled");

            if (!isEnabled)
            {
                _logger.LogTrace("Activity event tracking is disabled, skipping voice state change for user {UserId}", user.Id);
                activity?.SetTag("activity_tracking.enabled", false);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            var repository = scope.ServiceProvider.GetRequiredService<IUserActivityEventRepository>();

            // Detect voice channel join (user was not in a voice channel, now is)
            if (before.VoiceChannel == null && after.VoiceChannel != null)
            {
                var guildChannel = after.VoiceChannel as SocketGuildChannel;
                if (guildChannel == null)
                {
                    _logger.LogTrace("Skipping non-guild voice channel join for user {UserId}", user.Id);
                    BotActivitySource.SetSuccess(activity);
                    return;
                }

                _logger.LogDebug("Logging activity event: VoiceJoin from user {UserId} in guild {GuildId}",
                    user.Id, guildChannel.Guild.Id);

                var joinEvent = new UserActivityEvent
                {
                    UserId = user.Id,
                    GuildId = guildChannel.Guild.Id,
                    ChannelId = after.VoiceChannel.Id,
                    EventType = ActivityEventType.VoiceJoin,
                    Timestamp = DateTime.UtcNow,
                    LoggedAt = DateTime.UtcNow
                };

                await repository.AddAsync(joinEvent);

                _logger.LogDebug("Successfully logged VoiceJoin activity event for user {UserId} in guild {GuildId}",
                    user.Id, guildChannel.Guild.Id);
            }
            // Detect voice channel leave (user was in a voice channel, now is not)
            else if (before.VoiceChannel != null && after.VoiceChannel == null)
            {
                var guildChannel = before.VoiceChannel as SocketGuildChannel;
                if (guildChannel == null)
                {
                    _logger.LogTrace("Skipping non-guild voice channel leave for user {UserId}", user.Id);
                    BotActivitySource.SetSuccess(activity);
                    return;
                }

                _logger.LogDebug("Logging activity event: VoiceLeave from user {UserId} in guild {GuildId}",
                    user.Id, guildChannel.Guild.Id);

                var leaveEvent = new UserActivityEvent
                {
                    UserId = user.Id,
                    GuildId = guildChannel.Guild.Id,
                    ChannelId = before.VoiceChannel.Id,
                    EventType = ActivityEventType.VoiceLeave,
                    Timestamp = DateTime.UtcNow,
                    LoggedAt = DateTime.UtcNow
                };

                await repository.AddAsync(leaveEvent);

                _logger.LogDebug("Successfully logged VoiceLeave activity event for user {UserId} in guild {GuildId}",
                    user.Id, guildChannel.Guild.Id);
            }
            // User switched channels - log both leave and join
            else if (before.VoiceChannel != null && after.VoiceChannel != null && before.VoiceChannel.Id != after.VoiceChannel.Id)
            {
                var beforeGuildChannel = before.VoiceChannel as SocketGuildChannel;
                var afterGuildChannel = after.VoiceChannel as SocketGuildChannel;

                if (beforeGuildChannel != null && afterGuildChannel != null)
                {
                    _logger.LogDebug("Logging activity events: VoiceLeave and VoiceJoin from user {UserId} in guild {GuildId}",
                        user.Id, beforeGuildChannel.Guild.Id);

                    // Log leave from previous channel
                    var leaveEvent = new UserActivityEvent
                    {
                        UserId = user.Id,
                        GuildId = beforeGuildChannel.Guild.Id,
                        ChannelId = before.VoiceChannel.Id,
                        EventType = ActivityEventType.VoiceLeave,
                        Timestamp = DateTime.UtcNow,
                        LoggedAt = DateTime.UtcNow
                    };

                    await repository.AddAsync(leaveEvent);

                    // Log join to new channel
                    var joinEvent = new UserActivityEvent
                    {
                        UserId = user.Id,
                        GuildId = afterGuildChannel.Guild.Id,
                        ChannelId = after.VoiceChannel.Id,
                        EventType = ActivityEventType.VoiceJoin,
                        Timestamp = DateTime.UtcNow,
                        LoggedAt = DateTime.UtcNow
                    };

                    await repository.AddAsync(joinEvent);

                    _logger.LogDebug("Successfully logged VoiceLeave and VoiceJoin activity events for user {UserId}",
                        user.Id);
                }
            }

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log voice state change activity event for user {UserId}", user.Id);
            BotActivitySource.RecordException(activity, ex);
        }
    }

    /// <summary>
    /// Handles the UserJoined event from DiscordSocketClient.
    /// Logs guild join activity metadata.
    /// </summary>
    public async Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        using var activity = BotActivitySource.StartEventActivity(
            TracingConstants.Spans.DiscordEventMemberJoined,
            guildId: user.Guild.Id,
            userId: user.Id);

        try
        {
            // Filter out bots
            if (user.IsBot || user.IsWebhook)
            {
                _logger.LogTrace("Skipping bot join for user {UserId}", user.Id);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            // Check if activity event tracking is enabled
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var isEnabled = await settingsService.GetSettingValueAsync<bool>("Features:ActivityEventTrackingEnabled");

            if (!isEnabled)
            {
                _logger.LogTrace("Activity event tracking is disabled, skipping guild join for user {UserId}", user.Id);
                activity?.SetTag("activity_tracking.enabled", false);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            _logger.LogDebug("Logging activity event: GuildJoin from user {UserId} in guild {GuildId}",
                user.Id, user.Guild.Id);

            var repository = scope.ServiceProvider.GetRequiredService<IUserActivityEventRepository>();

            var activityEvent = new UserActivityEvent
            {
                UserId = user.Id,
                GuildId = user.Guild.Id,
                ChannelId = user.Guild.Id, // Use guild ID as channel ID for guild-level events
                EventType = ActivityEventType.GuildJoin,
                Timestamp = user.JoinedAt?.UtcDateTime ?? DateTime.UtcNow,
                LoggedAt = DateTime.UtcNow
            };

            await repository.AddAsync(activityEvent);

            _logger.LogDebug("Successfully logged GuildJoin activity event for user {UserId} in guild {GuildId}",
                user.Id, user.Guild.Id);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log GuildJoin activity event for user {UserId}", user.Id);
            BotActivitySource.RecordException(activity, ex);
        }
    }

    /// <summary>
    /// Handles the UserLeft event from DiscordSocketClient.
    /// Logs guild leave activity metadata.
    /// </summary>
    public async Task HandleUserLeftAsync(SocketGuild guild, SocketUser user)
    {
        using var activity = BotActivitySource.StartEventActivity(
            TracingConstants.Spans.DiscordEventMemberLeft,
            guildId: guild.Id,
            userId: user.Id);

        try
        {
            // Filter out bots
            if (user.IsBot || user.IsWebhook)
            {
                _logger.LogTrace("Skipping bot leave for user {UserId}", user.Id);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            // Check if activity event tracking is enabled
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var isEnabled = await settingsService.GetSettingValueAsync<bool>("Features:ActivityEventTrackingEnabled");

            if (!isEnabled)
            {
                _logger.LogTrace("Activity event tracking is disabled, skipping guild leave for user {UserId}", user.Id);
                activity?.SetTag("activity_tracking.enabled", false);
                BotActivitySource.SetSuccess(activity);
                return;
            }

            _logger.LogDebug("Logging activity event: GuildLeave from user {UserId} in guild {GuildId}",
                user.Id, guild.Id);

            var repository = scope.ServiceProvider.GetRequiredService<IUserActivityEventRepository>();

            var activityEvent = new UserActivityEvent
            {
                UserId = user.Id,
                GuildId = guild.Id,
                ChannelId = guild.Id, // Use guild ID as channel ID for guild-level events
                EventType = ActivityEventType.GuildLeave,
                Timestamp = DateTime.UtcNow,
                LoggedAt = DateTime.UtcNow
            };

            await repository.AddAsync(activityEvent);

            _logger.LogDebug("Successfully logged GuildLeave activity event for user {UserId} in guild {GuildId}",
                user.Id, guild.Id);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log GuildLeave activity event for user {UserId}", user.Id);
            BotActivitySource.RecordException(activity, ex);
        }
    }
}
