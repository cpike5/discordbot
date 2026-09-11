using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Services.Realtime.Events;

/// <summary>
/// Dual-publish of <see cref="Services.AudioNotifier.NotifyAudioConnectedAsync"/>
/// ("AudioConnected", sent to the <c>guild-audio-{guildId}</c> group).
/// </summary>
public sealed record AudioConnectedEvent : GuildScopedEvent
{
    public required AudioConnectedDto Data { get; init; }
}

/// <summary>
/// Dual-publish of <see cref="Services.AudioNotifier.NotifyAudioDisconnectedAsync"/>
/// ("AudioDisconnected", sent to the <c>guild-audio-{guildId}</c> group).
/// </summary>
public sealed record AudioDisconnectedEvent : GuildScopedEvent
{
    public required AudioDisconnectedDto Data { get; init; }
}

/// <summary>
/// Dual-publish of <see cref="Services.AudioNotifier.NotifyPlaybackStartedAsync"/>
/// ("PlaybackStarted", sent to the <c>guild-audio-{guildId}</c> group).
/// </summary>
public sealed record PlaybackStartedEvent : GuildScopedEvent
{
    public required PlaybackStartedDto Data { get; init; }
}

/// <summary>
/// Dual-publish of <see cref="Services.AudioNotifier.NotifyPlaybackProgressAsync"/>
/// ("PlaybackProgress", sent to the <c>guild-audio-{guildId}</c> group). Fires frequently
/// during playback; subscribers should debounce/coalesce their re-render.
/// </summary>
public sealed record PlaybackProgressEvent : GuildScopedEvent
{
    public required PlaybackProgressDto Data { get; init; }
}

/// <summary>
/// Dual-publish of <see cref="Services.AudioNotifier.NotifyPlaybackFinishedAsync"/>
/// ("PlaybackFinished", sent to the <c>guild-audio-{guildId}</c> group).
/// </summary>
public sealed record PlaybackFinishedEvent : GuildScopedEvent
{
    public required PlaybackFinishedDto Data { get; init; }
}

/// <summary>
/// Dual-publish of <see cref="Services.AudioNotifier.NotifyQueueUpdatedAsync"/>
/// ("QueueUpdated", sent to the <c>guild-audio-{guildId}</c> group).
/// </summary>
public sealed record QueueUpdatedEvent : GuildScopedEvent
{
    public required QueueUpdatedDto Queue { get; init; }
}

/// <summary>
/// Dual-publish of <see cref="Services.AudioNotifier.NotifyVoiceChannelMemberCountUpdatedAsync"/>
/// ("VoiceChannelMemberCountUpdated", sent to the <c>guild-audio-{guildId}</c> group).
/// </summary>
public sealed record VoiceChannelMemberCountUpdatedEvent : GuildScopedEvent
{
    public required VoiceChannelMemberCountUpdatedDto Data { get; init; }
}

/// <summary>
/// Dual-publish of <see cref="Services.AudioNotifier.NotifySoundUploadedAsync"/>
/// ("SoundUploaded", sent to the <c>guild-audio-{guildId}</c> group).
/// </summary>
public sealed record SoundUploadedEvent : GuildScopedEvent
{
    public required SoundUploadedDto Data { get; init; }
}

/// <summary>
/// Dual-publish of <see cref="Services.AudioNotifier.NotifySoundDeletedAsync"/>
/// ("SoundDeleted", sent to the <c>guild-audio-{guildId}</c> group).
/// </summary>
public sealed record SoundDeletedEvent : GuildScopedEvent
{
    public required SoundDeletedDto Data { get; init; }
}
