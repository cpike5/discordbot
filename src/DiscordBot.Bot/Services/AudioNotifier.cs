using DiscordBot.Bot.Hubs;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for sending real-time audio status notifications to dashboard clients via SignalR.
/// Broadcasts audio events to guild-specific audio groups.
/// </summary>
public class AudioNotifier : IAudioNotifier
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<AudioNotifier> _logger;

    /// <summary>
    /// Event names for client-side handlers.
    /// </summary>
    public static class Events
    {
        /// <summary>Event fired when bot connects to a voice channel.</summary>
        public const string AudioConnected = "AudioConnected";

        /// <summary>Event fired when bot disconnects from a voice channel.</summary>
        public const string AudioDisconnected = "AudioDisconnected";

        /// <summary>Event fired when a sound starts playing.</summary>
        public const string PlaybackStarted = "PlaybackStarted";

        /// <summary>Event fired periodically during playback with progress info.</summary>
        public const string PlaybackProgress = "PlaybackProgress";

        /// <summary>Event fired when a sound finishes playing.</summary>
        public const string PlaybackFinished = "PlaybackFinished";

        /// <summary>Event fired when the playback queue changes.</summary>
        public const string QueueUpdated = "QueueUpdated";

        /// <summary>Event fired when voice channel member count changes.</summary>
        public const string VoiceChannelMemberCountUpdated = "VoiceChannelMemberCountUpdated";

        /// <summary>Event fired when a new sound is uploaded to the soundboard.</summary>
        public const string SoundUploaded = "SoundUploaded";

        /// <summary>Event fired when a sound is deleted from the soundboard.</summary>
        public const string SoundDeleted = "SoundDeleted";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioNotifier"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context for DashboardHub.</param>
    /// <param name="logger">The logger.</param>
    public AudioNotifier(
        IHubContext<DashboardHub> hubContext,
        ILogger<AudioNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task NotifyAudioConnectedAsync(
        ulong guildId,
        ulong channelId,
        string channelName,
        int memberCount,
        CancellationToken cancellationToken = default)
    {
        var groupName = DashboardHub.GetGuildAudioGroupName(guildId);
        var data = new AudioConnectedDto
        {
            GuildId = guildId,
            ChannelId = channelId,
            ChannelName = channelName,
            MemberCount = memberCount,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogDebug(
            "Broadcasting AudioConnected: GuildId={GuildId}, ChannelId={ChannelId}, ChannelName={ChannelName}, MemberCount={MemberCount}",
            guildId,
            channelId,
            channelName,
            memberCount);

        await _hubContext.Clients.Group(groupName).SendAsync(
            Events.AudioConnected,
            data,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifyAudioDisconnectedAsync(
        ulong guildId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var groupName = DashboardHub.GetGuildAudioGroupName(guildId);
        var data = new AudioDisconnectedDto
        {
            GuildId = guildId,
            Reason = reason,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogDebug(
            "Broadcasting AudioDisconnected: GuildId={GuildId}, Reason={Reason}",
            guildId,
            reason);

        await _hubContext.Clients.Group(groupName).SendAsync(
            Events.AudioDisconnected,
            data,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifyPlaybackStartedAsync(
        ulong guildId,
        Guid soundId,
        string name,
        double durationSeconds,
        CancellationToken cancellationToken = default)
    {
        var groupName = DashboardHub.GetGuildAudioGroupName(guildId);
        var data = new PlaybackStartedDto
        {
            GuildId = guildId,
            SoundId = soundId,
            Name = name,
            DurationSeconds = durationSeconds,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogDebug(
            "Broadcasting PlaybackStarted: GuildId={GuildId}, SoundId={SoundId}, Name={Name}, Duration={DurationSeconds}s",
            guildId,
            soundId,
            name,
            durationSeconds);

        await _hubContext.Clients.Group(groupName).SendAsync(
            Events.PlaybackStarted,
            data,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifyPlaybackProgressAsync(
        ulong guildId,
        Guid soundId,
        double positionSeconds,
        double durationSeconds,
        CancellationToken cancellationToken = default)
    {
        var groupName = DashboardHub.GetGuildAudioGroupName(guildId);
        var data = new PlaybackProgressDto
        {
            GuildId = guildId,
            SoundId = soundId,
            PositionSeconds = positionSeconds,
            DurationSeconds = durationSeconds,
            Timestamp = DateTime.UtcNow
        };

        // Use Trace level since this fires frequently during playback
        _logger.LogTrace(
            "Broadcasting PlaybackProgress: GuildId={GuildId}, SoundId={SoundId}, Position={PositionSeconds}s/{DurationSeconds}s",
            guildId,
            soundId,
            positionSeconds,
            durationSeconds);

        await _hubContext.Clients.Group(groupName).SendAsync(
            Events.PlaybackProgress,
            data,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifyPlaybackFinishedAsync(
        ulong guildId,
        Guid soundId,
        bool wasCancelled,
        CancellationToken cancellationToken = default)
    {
        var groupName = DashboardHub.GetGuildAudioGroupName(guildId);
        var data = new PlaybackFinishedDto
        {
            GuildId = guildId,
            SoundId = soundId,
            WasCancelled = wasCancelled,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogDebug(
            "Broadcasting PlaybackFinished: GuildId={GuildId}, SoundId={SoundId}, WasCancelled={WasCancelled}",
            guildId,
            soundId,
            wasCancelled);

        await _hubContext.Clients.Group(groupName).SendAsync(
            Events.PlaybackFinished,
            data,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifyQueueUpdatedAsync(
        ulong guildId,
        QueueUpdatedDto queue,
        CancellationToken cancellationToken = default)
    {
        var groupName = DashboardHub.GetGuildAudioGroupName(guildId);

        // Ensure guildId and timestamp are set
        queue.GuildId = guildId;
        queue.Timestamp = DateTime.UtcNow;

        _logger.LogDebug(
            "Broadcasting QueueUpdated: GuildId={GuildId}, QueueLength={QueueLength}",
            guildId,
            queue.Queue.Count);

        await _hubContext.Clients.Group(groupName).SendAsync(
            Events.QueueUpdated,
            queue,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifyVoiceChannelMemberCountUpdatedAsync(
        ulong guildId,
        ulong channelId,
        string channelName,
        int memberCount,
        CancellationToken cancellationToken = default)
    {
        var groupName = DashboardHub.GetGuildAudioGroupName(guildId);
        var data = new VoiceChannelMemberCountUpdatedDto
        {
            GuildId = guildId,
            ChannelId = channelId,
            ChannelName = channelName,
            MemberCount = memberCount,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogDebug(
            "Broadcasting VoiceChannelMemberCountUpdated: GuildId={GuildId}, ChannelId={ChannelId}, MemberCount={MemberCount}",
            guildId,
            channelId,
            memberCount);

        await _hubContext.Clients.Group(groupName).SendAsync(
            Events.VoiceChannelMemberCountUpdated,
            data,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifySoundUploadedAsync(
        ulong guildId,
        Guid soundId,
        string name,
        int playCount,
        CancellationToken cancellationToken = default)
    {
        var groupName = DashboardHub.GetGuildAudioGroupName(guildId);
        var data = new SoundUploadedDto
        {
            GuildId = guildId,
            SoundId = soundId,
            Name = name,
            PlayCount = playCount,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogDebug(
            "Broadcasting SoundUploaded: GuildId={GuildId}, SoundId={SoundId}, Name={Name}",
            guildId,
            soundId,
            name);

        await _hubContext.Clients.Group(groupName).SendAsync(
            Events.SoundUploaded,
            data,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task NotifySoundDeletedAsync(
        ulong guildId,
        Guid soundId,
        CancellationToken cancellationToken = default)
    {
        var groupName = DashboardHub.GetGuildAudioGroupName(guildId);
        var data = new SoundDeletedDto
        {
            GuildId = guildId,
            SoundId = soundId,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogDebug(
            "Broadcasting SoundDeleted: GuildId={GuildId}, SoundId={SoundId}",
            guildId,
            soundId);

        await _hubContext.Clients.Group(groupName).SendAsync(
            Events.SoundDeleted,
            data,
            cancellationToken);
    }
}
