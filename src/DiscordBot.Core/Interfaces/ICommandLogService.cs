using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for command log retrieval and statistics.
/// </summary>
public interface ICommandLogService
{
    /// <summary>
    /// Gets command logs with optional filtering and pagination.
    /// </summary>
    /// <param name="query">The query parameters for filtering and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated response containing command log entries.</returns>
    Task<PaginatedResponseDto<CommandLogDto>> GetLogsAsync(CommandLogQueryDto query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command usage statistics.
    /// </summary>
    /// <param name="since">Optional start date for statistics. If null, returns all-time statistics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping command names to their usage counts.</returns>
    Task<IDictionary<string, int>> GetCommandStatsAsync(DateTime? since = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single command log entry by its unique identifier.
    /// </summary>
    /// <param name="id">The command log identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command log DTO, or null if not found.</returns>
    Task<CommandLogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command counts grouped by guild since a specified date.
    /// </summary>
    /// <param name="since">Start date for counting commands.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping guild ID to command count.</returns>
    Task<IDictionary<ulong, int>> GetCommandCountsByGuildAsync(DateTime since, CancellationToken cancellationToken = default);
}
