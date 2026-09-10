using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

public interface IDmAssistantInteractionLogRepository : IRepository<DmAssistantInteractionLog>
{
    Task<IEnumerable<DmAssistantInteractionLog>> GetRecentByUserAsync(
        ulong userId, int limit, CancellationToken ct = default);

    Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate, CancellationToken ct = default);

    /// <summary>
    /// Deletes DM interaction logs older than the specified date, one batch at a time. Used by
    /// <c>AssistantInteractionLogRetentionService</c> so a large backlog does not delete in one
    /// unbounded transaction.
    /// </summary>
    /// <param name="cutoffDate">The cutoff date. Entries with Timestamp &lt; cutoffDate will be deleted.</param>
    /// <param name="batchSize">Maximum rows to delete in this call. Clamped to 1000.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of entries deleted (may be less than <paramref name="batchSize"/> when fewer remain).</returns>
    Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate, int batchSize, CancellationToken ct = default);
}
