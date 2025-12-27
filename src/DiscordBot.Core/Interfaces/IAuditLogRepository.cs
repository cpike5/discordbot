using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for managing audit log entries.
/// Provides methods for querying, filtering, and bulk inserting audit logs.
/// </summary>
public interface IAuditLogRepository : IRepository<AuditLog>
{
    /// <summary>
    /// Retrieves audit logs with comprehensive filtering and pagination support.
    /// </summary>
    /// <param name="query">The query parameters including filters, sorting, and pagination.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A tuple containing the list of matching audit logs and the total count of all matching entries.
    /// </returns>
    Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetLogsAsync(
        AuditLogQueryDto query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all audit log entries with a specific correlation ID.
    /// Used to trace related events that are part of the same operation.
    /// </summary>
    /// <param name="correlationId">The correlation ID to search for.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A read-only list of audit logs with the specified correlation ID, ordered by timestamp.</returns>
    Task<IReadOnlyList<AuditLog>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves recent audit log entries for a specific actor.
    /// Useful for displaying user activity history.
    /// </summary>
    /// <param name="actorId">The actor ID to search for.</param>
    /// <param name="limit">Maximum number of entries to return (default: 50).</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A read-only list of recent audit logs for the actor, ordered by timestamp descending.</returns>
    Task<IReadOnlyList<AuditLog>> GetRecentByActorAsync(
        string actorId,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts multiple audit log entries in a single database operation.
    /// Optimized for high-volume logging scenarios.
    /// </summary>
    /// <param name="logs">The collection of audit logs to insert.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BulkInsertAsync(
        IEnumerable<AuditLog> logs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves statistical information about audit logs.
    /// Used for dashboard displays and reporting.
    /// </summary>
    /// <param name="guildId">Optional guild ID to filter stats by specific guild.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>Statistical data about audit log entries.</returns>
    Task<AuditLogStatsDto> GetStatsAsync(
        ulong? guildId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes audit log entries older than the specified date.
    /// Used for log retention and cleanup operations.
    /// </summary>
    /// <param name="olderThan">The cutoff date. Entries with Timestamp &lt; olderThan will be deleted.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The number of entries deleted.</returns>
    Task<int> DeleteOlderThanAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default);
}
