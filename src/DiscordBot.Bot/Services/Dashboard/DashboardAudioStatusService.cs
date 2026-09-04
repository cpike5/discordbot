using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Dashboard;

/// <summary>
/// Default implementation of <see cref="IDashboardAudioStatusService"/>.
/// </summary>
public class DashboardAudioStatusService : IDashboardAudioStatusService
{
    private const string SignalRConnectionIdAttribute = "signalr.connection.id";

    private readonly IAudioService _audioService;
    private readonly IPlaybackService _playbackService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<DashboardAudioStatusService> _logger;

    public DashboardAudioStatusService(
        IAudioService audioService,
        IPlaybackService playbackService,
        DiscordSocketClient discordClient,
        ILogger<DashboardAudioStatusService> logger)
    {
        _audioService = audioService;
        _playbackService = playbackService;
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public AudioStatusDto GetCurrentAudioStatus(ulong guildId, string? connectionId, string? userName)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "get_current_audio_status");

        activity?.SetTag(TracingConstants.Attributes.UserId, userName);
        activity?.SetTag(SignalRConnectionIdAttribute, connectionId);
        activity?.SetTag(TracingConstants.Attributes.GuildId, guildId.ToString());

        try
        {
            _logger.LogDebug(
                "Audio status requested by client: ConnectionId={ConnectionId}, GuildId={GuildId}",
                connectionId,
                guildId);

            var isConnected = _audioService.IsConnected(guildId);
            var channelId = _audioService.GetConnectedChannelId(guildId);
            var isPlaying = _playbackService.IsPlaying(guildId);
            var queueLength = _playbackService.GetQueueLength(guildId);

            string? channelName = null;
            if (channelId.HasValue)
            {
                var guild = _discordClient.GetGuild(guildId);
                var channel = guild?.GetVoiceChannel(channelId.Value);
                channelName = channel?.Name;
            }

            var status = new AudioStatusDto
            {
                GuildId = guildId,
                IsConnected = isConnected,
                ChannelId = channelId,
                ChannelName = channelName,
                IsPlaying = isPlaying,
                QueueLength = queueLength,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogTrace(
                "Audio status retrieved: GuildId={GuildId}, IsConnected={IsConnected}, IsPlaying={IsPlaying}, QueueLength={QueueLength}",
                guildId,
                status.IsConnected,
                status.IsPlaying,
                status.QueueLength);

            BotActivitySource.SetSuccess(activity);
            return status;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }
}
