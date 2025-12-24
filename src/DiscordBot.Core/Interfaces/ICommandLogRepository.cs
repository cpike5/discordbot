using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for CommandLog entities with audit-specific operations.
/// </summary>
public interface ICommandLogRepository : IRepository<CommandLog>
{
    /// <summary>
    /// Gets command logs for a specific guild.
    /// </summary>
    Task<IReadOnlyList<CommandLog>> GetByGuildAsync(
        ulong guildId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command logs for a specific user.
    /// </summary>
    Task<IReadOnlyList<CommandLog>> GetByUserAsync(
        ulong userId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command logs for a specific command name.
    /// </summary>
    Task<IReadOnlyList<CommandLog>> GetByCommandNameAsync(
        string commandName,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command logs within a date range.
    /// </summary>
    Task<IReadOnlyList<CommandLog>> GetByDateRangeAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets failed command logs.
    /// </summary>
    Task<IReadOnlyList<CommandLog>> GetFailedCommandsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command usage statistics.
    /// </summary>
    Task<IDictionary<string, int>> GetCommandUsageStatsAsync(
        DateTime? since = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs a command execution.
    /// </summary>
    Task<CommandLog> LogCommandAsync(
        ulong? guildId,
        ulong userId,
        string commandName,
        string? parameters,
        int responseTimeMs,
        bool success,
        string? errorMessage = null,
        string? correlationId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command usage aggregated by day.
    /// </summary>
    Task<IReadOnlyList<UsageOverTimeDto>> GetUsageOverTimeAsync(
        DateTime start,
        DateTime end,
        ulong? guildId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets success/failure rate statistics.
    /// </summary>
    Task<CommandSuccessRateDto> GetSuccessRateAsync(
        DateTime? since = null,
        ulong? guildId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets response time performance metrics by command.
    /// </summary>
    Task<IReadOnlyList<CommandPerformanceDto>> GetCommandPerformanceAsync(
        DateTime? since = null,
        ulong? guildId = null,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command logs with filters applied at the database level with pagination.
    /// </summary>
    /// <param name="searchTerm">Search term for multi-field search across command name, username, and guild name.</param>
    /// <param name="guildId">Optional guild ID filter.</param>
    /// <param name="userId">Optional user ID filter.</param>
    /// <param name="commandName">Optional command name filter.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="successOnly">Optional filter for success status.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated result containing filtered command logs and total count.</returns>
    Task<(IReadOnlyList<CommandLog> Items, int TotalCount)> GetFilteredLogsAsync(
        string? searchTerm = null,
        ulong? guildId = null,
        ulong? userId = null,
        string? commandName = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool? successOnly = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
