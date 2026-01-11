using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for TtsMessage entities with TTS-specific operations.
/// Provides methods for logging TTS messages and querying usage statistics.
/// </summary>
public interface ITtsMessageRepository
{
    /// <summary>
    /// Adds a new TTS message record.
    /// </summary>
    /// <param name="message">The TTS message to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The added TTS message with generated ID.</returns>
    Task<TtsMessage> AddAsync(TtsMessage message, CancellationToken ct = default);

    /// <summary>
    /// Gets recent TTS messages for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="count">Maximum number of messages to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of recent TTS messages ordered by creation date descending.</returns>
    Task<IEnumerable<TtsMessage>> GetRecentByGuildAsync(
        ulong guildId,
        int count = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the count of TTS messages for a guild since a specific time.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="since">The start time for counting (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of TTS messages since the specified time.</returns>
    Task<int> GetMessageCountAsync(
        ulong guildId,
        DateTime since,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the total playback duration in seconds for a guild since a specific time.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="since">The start time for summing (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The total playback duration in seconds.</returns>
    Task<double> GetTotalPlaybackSecondsAsync(
        ulong guildId,
        DateTime since,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the count of unique users who sent TTS messages for a guild since a specific time.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="since">The start time for counting (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of unique users.</returns>
    Task<int> GetUniqueUserCountAsync(
        ulong guildId,
        DateTime since,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the most used voice for a guild since a specific time.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="since">The start time for analysis (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The voice identifier that was used most, or null if no messages.</returns>
    Task<string?> GetMostUsedVoiceAsync(
        ulong guildId,
        DateTime since,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the user who sent the most TTS messages for a guild since a specific time.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="since">The start time for analysis (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (userId, username, messageCount), or null if no messages.</returns>
    Task<(ulong UserId, string Username, int MessageCount)?> GetTopUserAsync(
        ulong guildId,
        DateTime since,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the count of TTS messages sent by a user in a guild within a time window.
    /// Used for rate limiting.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="since">The start time for counting (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of TTS messages sent by the user since the specified time.</returns>
    Task<int> GetUserMessageCountAsync(
        ulong guildId,
        ulong userId,
        DateTime since,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a TTS message by ID.
    /// </summary>
    /// <param name="id">The TTS message ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the message was deleted, false if not found.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Deletes TTS message records older than the specified cutoff date in batches.
    /// Used for retention cleanup operations.
    /// </summary>
    /// <param name="cutoff">The cutoff date. Entries with CreatedAt &lt; cutoff will be deleted.</param>
    /// <param name="batchSize">Maximum number of entries to delete in this batch.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of entries deleted in this batch.</returns>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, int batchSize, CancellationToken ct = default);
}
