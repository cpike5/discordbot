using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for AssistantInteractionLog entities.
/// Provides data access operations for assistant conversation history and audit trails.
/// </summary>
public interface IAssistantInteractionLogRepository : IRepository<AssistantInteractionLog>
{
    /// <summary>
    /// Gets recent interaction logs for a guild.
    /// Returns logs ordered by timestamp descending (most recent first).
    /// </summary>
    /// <param name="guildId">Discord guild ID to retrieve logs for.</param>
    /// <param name="limit">Maximum number of logs to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of recent interaction logs for the guild.</returns>
    Task<IEnumerable<AssistantInteractionLog>> GetRecentByGuildAsync(
        ulong guildId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets recent interaction logs for a user across all guilds.
    /// Returns logs ordered by timestamp descending (most recent first).
    /// </summary>
    /// <param name="userId">Discord user ID to retrieve logs for.</param>
    /// <param name="limit">Maximum number of logs to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of recent interaction logs for the user.</returns>
    Task<IEnumerable<AssistantInteractionLog>> GetRecentByUserAsync(
        ulong userId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts tool usage for a guild over a window, from the comma-joined
    /// <see cref="AssistantInteractionLog.ToolNames"/> column.
    /// </summary>
    /// <remarks>
    /// The column is a joined string rather than a join table, so the split and the grouping happen
    /// in memory over a narrow projection of the window's rows. That is deliberate: it needs no
    /// provider-specific string function and no second table, and a guild's 30-day window is a few
    /// thousand rows at most. A tool that was never called simply has no row here - the caller pairs
    /// this with the tool catalogue to show the zeroes, which is the question worth answering.
    /// </remarks>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="from">Start of the window, inclusive (UTC).</param>
    /// <param name="to">End of the window, inclusive (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One row per tool that was called, ordered by call count descending.</returns>
    Task<IReadOnlyList<AssistantToolUsage>> GetToolUsageAsync(
        ulong guildId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes interaction logs older than the specified date.
    /// Used for retention policy enforcement and database cleanup.
    /// </summary>
    /// <param name="cutoffDate">The cutoff date. Entries with Timestamp &lt; cutoffDate will be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of entries deleted.</returns>
    Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes interaction logs older than the specified date, one batch at a time. Used by
    /// <c>AssistantInteractionLogRetentionService</c> so a large backlog does not delete in one
    /// unbounded transaction.
    /// </summary>
    /// <param name="cutoffDate">The cutoff date. Entries with Timestamp &lt; cutoffDate will be deleted.</param>
    /// <param name="batchSize">Maximum rows to delete in this call. Clamped to 1000.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of entries deleted (may be less than <paramref name="batchSize"/> when fewer remain).</returns>
    Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate,
        int batchSize,
        CancellationToken cancellationToken = default);
}
