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
    /// Deletes interaction logs older than the specified date.
    /// Used for retention policy enforcement and database cleanup.
    /// </summary>
    /// <param name="cutoffDate">The cutoff date. Entries with Timestamp &lt; cutoffDate will be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of entries deleted.</returns>
    Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate,
        CancellationToken cancellationToken = default);
}
