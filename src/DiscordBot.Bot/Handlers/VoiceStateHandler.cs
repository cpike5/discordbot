using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Handles Discord voice state events (UserVoiceStateUpdated) to broadcast
/// real-time member count updates to connected portal clients via SignalR,
/// and keeps <see cref="IAudioService"/>'s tracked connections in step with the
/// voice state Discord reports for the bot itself.
/// </summary>
public class VoiceStateHandler
{
    private readonly DiscordSocketClient _client;
    private readonly IAudioService _audioService;
    private readonly IAudioNotifier _audioNotifier;
    private readonly ILogger<VoiceStateHandler> _logger;

    public VoiceStateHandler(
        DiscordSocketClient client,
        IAudioService audioService,
        IAudioNotifier audioNotifier,
        ILogger<VoiceStateHandler> logger)
    {
        _client = client;
        _audioService = audioService;
        _audioNotifier = audioNotifier;
        _logger = logger;
    }

    /// <summary>
    /// Handles the Ready event. After a (re)connect the gateway may no longer hold the voice state the
    /// bot had before, so every tracked connection is reconciled against what Discord now reports.
    /// </summary>
    public Task HandleReadyAsync()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _audioService.ReconcileAllBotVoiceStatesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reconcile bot voice states after Ready");
            }
        });

        return Task.CompletedTask;
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
        // Our own voice state changed (joined, left, kicked, moved): reconcile tracked state.
        // Run off the gateway thread and never block it: Discord.NET's ConnectAsync waits for
        // gateway events that are dispatched on this same thread, and the reconcile takes the
        // guild lock that the join holds.
        if (user.Id == _client.CurrentUser?.Id)
        {
            var guildId = (before.VoiceChannel ?? after.VoiceChannel)?.Guild.Id;
            if (guildId.HasValue)
            {
                _ = Task.Run(() => _audioService.ReconcileBotVoiceStateAsync(guildId.Value));
            }

            return;
        }

        // Skip other bots
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
