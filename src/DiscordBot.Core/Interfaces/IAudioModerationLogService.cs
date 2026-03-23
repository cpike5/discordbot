using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for logging audio playback events for moderation purposes.
/// All logging is fire-and-forget to avoid blocking the playback response.
/// </summary>
public interface IAudioModerationLogService
{
    /// <summary>
    /// Logs an audio playback event. This method is fire-and-forget and returns immediately.
    /// The actual database write happens asynchronously in the background.
    /// </summary>
    /// <param name="guildId">The Discord guild ID where the audio was played.</param>
    /// <param name="userId">The Discord user ID who triggered the playback.</param>
    /// <param name="featureType">The type of audio feature used.</param>
    /// <param name="contentName">The content name (sound name, TTS text preview, or VOX message). Will be truncated to 200 characters.</param>
    /// <param name="channelId">The voice channel ID where the audio was played, if available.</param>
    void LogPlayback(ulong guildId, ulong userId, AudioFeatureType featureType, string contentName, ulong? channelId);
}
