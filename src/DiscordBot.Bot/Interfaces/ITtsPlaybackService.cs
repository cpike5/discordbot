using DiscordBot.Core.DTOs.Tts;

namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Service interface for TTS playback orchestration.
/// Consolidates PCM streaming, duration calculation, history logging, and activity tracking.
/// </summary>
public interface ITtsPlaybackService
{
    /// <summary>
    /// Plays synthesized TTS audio to Discord voice channel.
    /// Handles PCM streaming, duration calculation, history logging, and observability.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="userId">The Discord user snowflake ID who triggered TTS.</param>
    /// <param name="username">The username for history logging.</param>
    /// <param name="message">The original text message (for history).</param>
    /// <param name="voice">The voice name used for synthesis.</param>
    /// <param name="audioStream">The synthesized audio stream (48kHz, 16-bit, stereo PCM).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Playback result containing success status, error message, duration, and logged message.</returns>
    Task<TtsPlaybackResult> PlayAsync(
        ulong guildId,
        ulong userId,
        string username,
        string message,
        string voice,
        Stream audioStream,
        CancellationToken cancellationToken = default);
}
