using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing audit log operations.
/// Provides methods for querying, retrieving, and logging audit entries.
/// </summary>
public interface IAuditLogService
{
    /// <summary>
    /// Retrieves audit logs with comprehensive filtering and pagination support.
    /// </summary>
    /// <param name="query">The query parameters including filters, sorting, and pagination.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>
    /// A tuple containing the list of matching audit logs and the total count of all matching entries.
    /// </returns>
    Task<(IReadOnlyList<AuditLogDto> Items, int TotalCount)> GetLogsAsync(
        AuditLogQueryDto query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single audit log entry by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the audit log entry.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The audit log DTO if found; otherwise, null.</returns>
    Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all audit log entries with a specific correlation ID.
    /// Used to trace related events that are part of the same operation.
    /// </summary>
    /// <param name="correlationId">The correlation ID to search for.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A read-only list of audit logs with the specified correlation ID, ordered by timestamp.</returns>
    Task<IReadOnlyList<AuditLogDto>> GetByCorrelationIdAsync(
        string correlationId,
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
    /// Logs an audit entry asynchronously without waiting for confirmation.
    /// This method writes to a background queue for high-performance, fire-and-forget logging.
    /// </summary>
    /// <param name="dto">The audit log data to create.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogAsync(AuditLogCreateDto dto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a fluent builder for constructing and logging audit entries.
    /// Provides a more readable, chainable API for creating audit logs.
    /// </summary>
    /// <returns>A new instance of the audit log builder.</returns>
    IAuditLogBuilder CreateBuilder();
}
