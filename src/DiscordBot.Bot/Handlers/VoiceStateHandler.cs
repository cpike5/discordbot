using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Handles Discord voice state events (UserVoiceStateUpdated) to broadcast
/// real-time member count updates to connected portal clients via SignalR.
/// </summary>
public class VoiceStateHandler
{
    private readonly IAudioService _audioService;
    private readonly IAudioNotifier _audioNotifier;
    private readonly ILogger<VoiceStateHandler> _logger;

    public VoiceStateHandler(
        IAudioService audioService,
        IAudioNotifier audioNotifier,
        ILogger<VoiceStateHandler> logger)
    {
        _audioService = audioService;
        _audioNotifier = audioNotifier;
        _logger = logger;
    }

    /// <summary>
    /// Handles the UserVoiceStateUpdated event. Broadcasts member count updates
    /// when users join or leave voice channels where the bot is connected.
    /// </summary>
    /// <param name="user">The user whose voice state changed.</param>
    /// <param name="before">The previous voice state.</param>
    /// <param name="after">The current voice state.</param>
    public async Task HandleUserVoiceStateUpdatedAsync(
        SocketUser user,
        SocketVoiceState before,
        SocketVoiceState after)
    {
        // Skip if user is a bot (including our own state changes)
        if (user.IsBot)
        {
            return;
        }

        // Get affected channel IDs
        var leftChannelId = before.VoiceChannel?.Id;
        var joinedChannelId = after.VoiceChannel?.Id;

        // Skip if no channel change
        if (leftChannelId == joinedChannelId)
        {
            return;
        }

        // Handle user leaving a channel
        if (leftChannelId.HasValue)
        {
            await NotifyIfBotConnectedAsync(before.VoiceChannel!);
        }

        // Handle user joining a channel
        if (joinedChannelId.HasValue)
        {
            await NotifyIfBotConnectedAsync(after.VoiceChannel!);
        }
    }

    /// <summary>
    /// Sends a member count update if the bot is connected to the given channel.
    /// </summary>
    private async Task NotifyIfBotConnectedAsync(SocketVoiceChannel channel)
    {
        var guildId = channel.Guild.Id;

        // Check if bot is connected to this guild
        if (!_audioService.IsConnected(guildId))
        {
            return;
        }

        // Check if bot is connected to this specific channel
        var connectedChannelId = _audioService.GetConnectedChannelId(guildId);
        if (connectedChannelId != channel.Id)
        {
            return;
        }

        using var activity = BotActivitySource.StartEventActivity(
            "voice_member_count_updated",
            guildId: guildId);

        try
        {
            // Count members excluding bots
            var memberCount = channel.ConnectedUsers.Count(u => !u.IsBot);

            _logger.LogDebug(
                "Broadcasting voice channel member count update: GuildId={GuildId}, ChannelId={ChannelId}, MemberCount={MemberCount}",
                guildId,
                channel.Id,
                memberCount);

            activity?.SetTag(TracingConstants.Attributes.VoiceChannelId, channel.Id.ToString());
            activity?.SetTag(TracingConstants.Attributes.VoiceChannelName, channel.Name);
            activity?.SetTag("voice.member_count", memberCount);

            await _audioNotifier.NotifyVoiceChannelMemberCountUpdatedAsync(
                guildId,
                channel.Id,
                channel.Name,
                memberCount);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to broadcast voice channel member count update for channel {ChannelId} in guild {GuildId}",
                channel.Id, guildId);
            BotActivitySource.RecordException(activity, ex);
        }
    }
}
