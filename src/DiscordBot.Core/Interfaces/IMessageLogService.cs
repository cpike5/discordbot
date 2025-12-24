using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for message log retrieval, statistics, and data management.
/// </summary>
public interface IMessageLogService
{
    /// <summary>
    /// Gets message logs with optional filtering and pagination.
    /// </summary>
    /// <param name="query">The query parameters for filtering and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated response containing message log entries.</returns>
    Task<PaginatedResponseDto<MessageLogDto>> GetLogsAsync(MessageLogQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single message log entry by its unique identifier.
    /// </summary>
    /// <param name="id">The message log identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The message log DTO, or null if not found.</returns>
    Task<MessageLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets comprehensive message statistics including counts, breakdowns, and trends.
    /// </summary>
    /// <param name="guildId">Optional guild ID to filter statistics. If null, returns global statistics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Message statistics DTO containing aggregate data and trends.</returns>
    Task<MessageLogStatsDto> GetStatsAsync(ulong? guildId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all message logs for a specific user.
    /// Used for GDPR compliance and user data deletion requests.
    /// </summary>
    /// <param name="userId">The Discord user ID whose messages should be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of message logs deleted.</returns>
    Task<int> DeleteUserMessagesAsync(ulong userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old message logs according to the configured retention policy.
    /// Deletes messages older than the retention period in batches.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of message logs deleted.</returns>
    Task<int> CleanupOldMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports message logs matching the query criteria to a CSV file.
    /// </summary>
    /// <param name="query">The query parameters for filtering which messages to export.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Byte array containing the CSV file data.</returns>
    Task<byte[]> ExportToCsvAsync(MessageLogQueryDto query, CancellationToken cancellationToken = default);
}
