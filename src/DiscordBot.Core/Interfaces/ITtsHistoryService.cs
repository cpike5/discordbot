using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing TTS message history and statistics.
/// </summary>
public interface ITtsHistoryService
{
    /// <summary>
    /// Logs a new TTS message to the history.
    /// </summary>
    /// <param name="message">The TTS message to log.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogMessageAsync(TtsMessage message, CancellationToken ct = default);

    /// <summary>
    /// Gets recent TTS messages for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="count">Maximum number of messages to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of recent TTS message DTOs.</returns>
    Task<IEnumerable<TtsMessageDto>> GetRecentMessagesAsync(
        ulong guildId,
        int count = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Gets TTS statistics for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Statistics DTO with message counts, playback duration, and top users.</returns>
    Task<TtsStatsDto> GetStatsAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a TTS message from history.
    /// </summary>
    /// <param name="id">The TTS message ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the message was deleted, false if not found.</returns>
    Task<bool> DeleteMessageAsync(Guid id, CancellationToken ct = default);
}
