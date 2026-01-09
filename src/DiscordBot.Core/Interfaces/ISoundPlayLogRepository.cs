using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for managing sound play log entries.
/// Provides methods for logging plays and querying play statistics.
/// </summary>
public interface ISoundPlayLogRepository
{
    /// <summary>
    /// Adds a new sound play log entry.
    /// </summary>
    /// <param name="log">The play log entry to add.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(SoundPlayLog log, CancellationToken ct = default);

    /// <summary>
    /// Gets the total play count for a guild since a specific time.
    /// </summary>
    /// <param name="guildId">The Discord guild ID.</param>
    /// <param name="since">The start time for the count (UTC).</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The number of plays since the specified time.</returns>
    Task<int> GetPlayCountAsync(ulong guildId, DateTime since, CancellationToken ct = default);

    /// <summary>
    /// Gets the play count for a specific sound since a specific time.
    /// </summary>
    /// <param name="soundId">The sound identifier.</param>
    /// <param name="since">The start time for the count (UTC).</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The number of plays for this sound since the specified time.</returns>
    Task<int> GetPlayCountForSoundAsync(Guid soundId, DateTime since, CancellationToken ct = default);

    /// <summary>
    /// Deletes play log entries older than the specified cutoff date in batches.
    /// Used for retention cleanup operations.
    /// </summary>
    /// <param name="cutoff">The cutoff date. Entries with PlayedAt &lt; cutoff will be deleted.</param>
    /// <param name="batchSize">Maximum number of entries to delete in this batch.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>The number of entries deleted in this batch.</returns>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, int batchSize, CancellationToken ct = default);
}
