using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Handles streaming audio data to a Discord PCM output stream.
/// Routes between cached audio and FFmpeg transcoding, and manages
/// progress notifications during playback.
/// </summary>
public interface IAudioStreamer
{
    /// <summary>
    /// Streams audio for the given sound to the Discord output stream.
    /// Attempts to use cached audio first; falls back to FFmpeg transcoding.
    /// If a filter causes an FFmpeg error, automatically retries without the filter.
    /// </summary>
    /// <param name="guildId">The guild ID for logging and notifications.</param>
    /// <param name="sound">The sound entity to stream.</param>
    /// <param name="filePath">The absolute path to the audio file on disk.</param>
    /// <param name="filter">The audio filter to apply.</param>
    /// <param name="discord">The Discord PCM output stream to write to.</param>
    /// <param name="cancellationToken">Cancellation token for stopping playback.</param>
    /// <returns>A result indicating whether playback succeeded and whether it was cancelled.</returns>
    Task<AudioStreamResult> StreamAsync(
        ulong guildId,
        Sound sound,
        string filePath,
        AudioFilter filter,
        Stream discord,
        CancellationToken cancellationToken);
}

/// <summary>
/// Represents the result of an audio streaming operation.
/// </summary>
public record AudioStreamResult
{
    /// <summary>
    /// Whether the streaming completed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Whether the streaming was cancelled (e.g., by a stop command).
    /// </summary>
    public required bool WasCancelled { get; init; }
}
