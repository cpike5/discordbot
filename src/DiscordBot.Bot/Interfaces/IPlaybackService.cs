using DiscordBot.Core.Entities;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Service interface for soundboard audio playback.
/// Handles FFmpeg-based audio streaming to Discord voice channels with queue/replace modes.
/// </summary>
public interface IPlaybackService
{
    /// <summary>
    /// Plays a sound in the specified guild's voice channel.
    /// Behavior depends on the queueEnabled parameter:
    /// - If true: Sound is added to the queue and played when current playback finishes.
    /// - If false: Current playback is stopped and the new sound plays immediately.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="sound">The sound entity to play.</param>
    /// <param name="queueEnabled">Whether to queue the sound or replace current playback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// Requires an active voice connection via IAudioService.JoinChannelAsync before calling.
    /// The audio file is transcoded using FFmpeg to Opus PCM format (48kHz, stereo, 16-bit).
    /// Updates the sound's play count and last activity timestamp on the voice connection.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if no audio client is available for the guild.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the sound file does not exist.</exception>
    Task PlayAsync(ulong guildId, Sound sound, bool queueEnabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the currently playing sound in the specified guild and clears the queue.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// If no sound is currently playing, this method completes successfully without error.
    /// </remarks>
    Task StopAsync(ulong guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a sound is currently playing in the specified guild.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <returns>True if a sound is currently playing, false otherwise.</returns>
    bool IsPlaying(ulong guildId);

    /// <summary>
    /// Gets the number of sounds queued for playback in the specified guild.
    /// Does not include the currently playing sound.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <returns>The number of sounds in the queue.</returns>
    int GetQueueLength(ulong guildId);

    /// <summary>
    /// Removes a sound from the queue at the specified position.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="position">Zero-based position in the queue to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the item was removed, false if the position was invalid.</returns>
    /// <remarks>
    /// If the position is 0 and a sound is currently playing, the current sound will be skipped.
    /// Position 0 represents the currently playing or next-to-play sound.
    /// </remarks>
    Task<bool> RemoveFromQueueAsync(ulong guildId, int position, CancellationToken cancellationToken = default);
}
