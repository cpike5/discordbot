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
}
