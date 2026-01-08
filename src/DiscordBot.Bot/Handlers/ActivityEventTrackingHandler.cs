using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Handles Discord events and tracks anonymous user activity events for consent-free analytics.
/// Captures activity metadata (who, where, when, what type) without storing any message content.
/// Legal basis: GDPR Article 6.1.f (legitimate interest) - metadata counts are not privacy-invasive.
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
    /// Tracks message activity without storing content.
    /// </summary>
    /// <param name="message">The received message from Discord.</param>
    public async Task HandleMessageReceivedAsync(SocketMessage message)
    {
        using var activity = BotActivitySource.StartEventActivity(
            TracingConstants.Spans.ActivityEventTracking,
            guildId: (message.Channel as SocketGuildChannel)?.Guild.Id,
            channelId: message.Channel.Id,
            userId: message.Author.Id);

        try
        {
            // Filter out bot messages - don't track bot activity
            if (message.Author.IsBot)
            {
                BotActivitySource.SetSuccess(activity);
                return;
            }

            // Only track guild messages (not DMs) for analytics
            if (message.Channel is not SocketGuildChannel guildChannel)
            {
                BotActivitySource.SetSuccess(activity);
                return;
            }

            // Filter out system messages - only track user messages
            if (message is not SocketUserMessage userMessage || userMessage.Source != Discord.MessageSource.User)
            {
                BotActivitySource.SetSuccess(activity);
                return;
            }

            var guildId = guildChannel.Guild.Id;
            var channelId = message.Channel.Id;
            var userId = message.Author.Id;
            var timestamp = message.Timestamp.UtcDateTime;

            // Determine event type based on message characteristics
            var eventType = ActivityEventType.Message;

            // If this is a reply, track as Reply event
            if (message.Reference?.MessageId.IsSpecified == true)
            {
                eventType = ActivityEventType.Reply;
            }

            // Create the activity event (NO content stored)
            var activityEvent = new UserActivityEvent
            {
                UserId = userId,
                GuildId = guildId,
                ChannelId = channelId,
                Timestamp = timestamp,
                EventType = eventType
            };

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IUserActivityEventRepository>();
            await repository.AddAsync(activityEvent);

            _logger.LogTrace(
                "Tracked {EventType} activity for user {UserId} in guild {GuildId} channel {ChannelId}",
                eventType, userId, guildId, channelId);

            // Track additional events for message characteristics
            var additionalEvents = new List<UserActivityEvent>();

            // Track attachment event if message has attachments
            if (message.Attachments.Count > 0)
            {
                additionalEvents.Add(new UserActivityEvent
                {
                    UserId = userId,
                    GuildId = guildId,
                    ChannelId = channelId,
                    Timestamp = timestamp,
                    EventType = ActivityEventType.Attachment
                });
            }

            // Track mention events (one per message with mentions, not per mention)
            if (message.MentionedUsers.Count > 0 || message.MentionedRoles.Count > 0)
            {
                additionalEvents.Add(new UserActivityEvent
                {
                    UserId = userId,
                    GuildId = guildId,
                    ChannelId = channelId,
                    Timestamp = timestamp,
                    EventType = ActivityEventType.Mention
                });
            }

            if (additionalEvents.Count > 0)
            {
                await repository.AddBatchAsync(additionalEvents);
            }

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to track activity event for message {MessageId} from user {AuthorId}",
                message.Id, message.Author.Id);
            BotActivitySource.RecordException(activity, ex);
        }
    }

    /// <summary>
    /// Handles the ReactionAdded event from DiscordSocketClient.
    /// Tracks reaction activity.
    /// </summary>
    public async Task HandleReactionAddedAsync(
        Cacheable<IUserMessage, ulong> message,
        Cacheable<IMessageChannel, ulong> channel,
        SocketReaction reaction)
    {
        using var activity = BotActivitySource.StartEventActivity(
            TracingConstants.Spans.ActivityEventTracking,
            channelId: channel.Id,
            userId: reaction.UserId);

        try
        {
            // Filter out bot reactions
            if (reaction.User.IsSpecified && reaction.User.Value.IsBot)
            {
                BotActivitySource.SetSuccess(activity);
                return;
            }

            // Only track guild channel reactions
            var resolvedChannel = channel.HasValue ? channel.Value : await channel.GetOrDownloadAsync();
            if (resolvedChannel is not SocketGuildChannel guildChannel)
            {
                BotActivitySource.SetSuccess(activity);
                return;
            }

            var activityEvent = new UserActivityEvent
            {
                UserId = reaction.UserId,
                GuildId = guildChannel.Guild.Id,
                ChannelId = channel.Id,
                Timestamp = DateTime.UtcNow,
                EventType = ActivityEventType.Reaction
            };

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IUserActivityEventRepository>();
            await repository.AddAsync(activityEvent);

            _logger.LogTrace(
                "Tracked Reaction activity for user {UserId} in guild {GuildId} channel {ChannelId}",
                reaction.UserId, guildChannel.Guild.Id, channel.Id);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to track reaction event for user {UserId} in channel {ChannelId}",
                reaction.UserId, channel.Id);
            BotActivitySource.RecordException(activity, ex);
        }
    }

    /// <summary>
    /// Handles the UserVoiceStateUpdated event from DiscordSocketClient.
    /// Tracks voice channel join/leave activity.
    /// </summary>
    public async Task HandleVoiceStateUpdatedAsync(
        SocketUser user,
        SocketVoiceState before,
        SocketVoiceState after)
    {
        using var activity = BotActivitySource.StartEventActivity(
            TracingConstants.Spans.ActivityEventTracking,
            guildId: after.VoiceChannel?.Guild.Id ?? before.VoiceChannel?.Guild.Id,
            userId: user.Id);

        try
        {
            // Filter out bot voice state changes
            if (user.IsBot)
            {
                BotActivitySource.SetSuccess(activity);
                return;
            }

            var events = new List<UserActivityEvent>();

            // User joined a voice channel
            if (before.VoiceChannel == null && after.VoiceChannel != null)
            {
                events.Add(new UserActivityEvent
                {
                    UserId = user.Id,
                    GuildId = after.VoiceChannel.Guild.Id,
                    ChannelId = after.VoiceChannel.Id,
                    Timestamp = DateTime.UtcNow,
                    EventType = ActivityEventType.VoiceJoin
                });

                _logger.LogTrace(
                    "Tracked VoiceJoin for user {UserId} in guild {GuildId} channel {ChannelId}",
                    user.Id, after.VoiceChannel.Guild.Id, after.VoiceChannel.Id);
            }
            // User left a voice channel
            else if (before.VoiceChannel != null && after.VoiceChannel == null)
            {
                events.Add(new UserActivityEvent
                {
                    UserId = user.Id,
                    GuildId = before.VoiceChannel.Guild.Id,
                    ChannelId = before.VoiceChannel.Id,
                    Timestamp = DateTime.UtcNow,
                    EventType = ActivityEventType.VoiceLeave
                });

                _logger.LogTrace(
                    "Tracked VoiceLeave for user {UserId} in guild {GuildId} channel {ChannelId}",
                    user.Id, before.VoiceChannel.Guild.Id, before.VoiceChannel.Id);
            }
            // User moved between voice channels
            else if (before.VoiceChannel != null && after.VoiceChannel != null &&
                     before.VoiceChannel.Id != after.VoiceChannel.Id)
            {
                // Track leave from old channel
                events.Add(new UserActivityEvent
                {
                    UserId = user.Id,
                    GuildId = before.VoiceChannel.Guild.Id,
                    ChannelId = before.VoiceChannel.Id,
                    Timestamp = DateTime.UtcNow,
                    EventType = ActivityEventType.VoiceLeave
                });

                // Track join to new channel
                events.Add(new UserActivityEvent
                {
                    UserId = user.Id,
                    GuildId = after.VoiceChannel.Guild.Id,
                    ChannelId = after.VoiceChannel.Id,
                    Timestamp = DateTime.UtcNow,
                    EventType = ActivityEventType.VoiceJoin
                });

                _logger.LogTrace(
                    "Tracked voice channel move for user {UserId}: {OldChannel} -> {NewChannel}",
                    user.Id, before.VoiceChannel.Id, after.VoiceChannel.Id);
            }

            if (events.Count > 0)
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IUserActivityEventRepository>();
                await repository.AddBatchAsync(events);
            }

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to track voice state update for user {UserId}",
                user.Id);
            BotActivitySource.RecordException(activity, ex);
        }
    }

    /// <summary>
    /// Handles slash command execution by tracking SlashCommand activity events.
    /// This should be called from the InteractionHandler after command execution.
    /// </summary>
    public async Task TrackSlashCommandAsync(
        ulong userId,
        ulong guildId,
        ulong channelId)
    {
        try
        {
            var activityEvent = new UserActivityEvent
            {
                UserId = userId,
                GuildId = guildId,
                ChannelId = channelId,
                Timestamp = DateTime.UtcNow,
                EventType = ActivityEventType.SlashCommand
            };

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IUserActivityEventRepository>();
            await repository.AddAsync(activityEvent);

            _logger.LogTrace(
                "Tracked SlashCommand activity for user {UserId} in guild {GuildId}",
                userId, guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to track slash command event for user {UserId} in guild {GuildId}",
                userId, guildId);
        }
    }
}
