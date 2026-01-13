using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Interface for sending real-time audio status notifications to dashboard clients.
/// Used by audio services to broadcast voice connection and playback events through SignalR.
/// </summary>
public interface IAudioNotifier
{
    /// <summary>
    /// Notifies clients that the bot has connected to a voice channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The voice channel ID.</param>
    /// <param name="channelName">The voice channel name.</param>
    /// <param name="memberCount">The number of members in the voice channel (excluding bots).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyAudioConnectedAsync(
        ulong guildId,
        ulong channelId,
        string channelName,
        int memberCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that the bot has disconnected from a voice channel.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="reason">The reason for disconnection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyAudioDisconnectedAsync(
        ulong guildId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that playback has started for a sound.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="soundId">The sound ID.</param>
    /// <param name="name">The sound name.</param>
    /// <param name="durationSeconds">The sound duration in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyPlaybackStartedAsync(
        ulong guildId,
        Guid soundId,
        string name,
        double durationSeconds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients of playback progress during sound playback.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="soundId">The sound ID.</param>
    /// <param name="positionSeconds">The current playback position in seconds.</param>
    /// <param name="durationSeconds">The total duration in seconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyPlaybackProgressAsync(
        ulong guildId,
        Guid soundId,
        double positionSeconds,
        double durationSeconds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that playback has finished for a sound.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="soundId">The sound ID.</param>
    /// <param name="wasCancelled">Whether playback was cancelled (vs. completed naturally).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyPlaybackFinishedAsync(
        ulong guildId,
        Guid soundId,
        bool wasCancelled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that the playback queue has been updated.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="queue">The updated queue.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyQueueUpdatedAsync(
        ulong guildId,
        QueueUpdatedDto queue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that the voice channel member count has changed.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="channelId">The voice channel ID.</param>
    /// <param name="channelName">The voice channel name.</param>
    /// <param name="memberCount">The updated number of members in the channel (excluding bots).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifyVoiceChannelMemberCountUpdatedAsync(
        ulong guildId,
        ulong channelId,
        string channelName,
        int memberCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that a new sound has been uploaded to the soundboard.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="soundId">The sound ID.</param>
    /// <param name="name">The sound name.</param>
    /// <param name="playCount">The play count (0 for new sounds).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifySoundUploadedAsync(
        ulong guildId,
        Guid soundId,
        string name,
        int playCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that a sound has been deleted from the soundboard.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="soundId">The sound ID that was deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NotifySoundDeletedAsync(
        ulong guildId,
        Guid soundId,
        CancellationToken cancellationToken = default);
}
