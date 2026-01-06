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
    /// <param name="start">Start date of the range.</param>
    /// <param name="end">End date of the range.</param>
    /// <param name="includeDetails">If true, includes User and Guild navigation properties.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<CommandLog>> GetByDateRangeAsync(
        DateTime start,
        DateTime end,
        bool includeDetails = false,
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

    /// <summary>
    /// Gets the count of unique users who have executed commands since a specified date.
    /// </summary>
    /// <param name="since">Start date for counting unique users.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of unique users.</returns>
    Task<int> GetUniqueUserCountAsync(DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of guilds with command activity since a specified date.
    /// </summary>
    /// <param name="since">Start date for counting active guilds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of active guilds.</returns>
    Task<int> GetActiveGuildCountAsync(DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of commands executed since a specified date.
    /// </summary>
    /// <param name="since">Start date for counting commands.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total command count.</returns>
    Task<int> GetCommandCountAsync(DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets command counts grouped by guild since a specified date.
    /// </summary>
    /// <param name="since">Start date for counting commands.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary mapping guild ID to command count.</returns>
    Task<IDictionary<ulong, int>> GetCommandCountsByGuildAsync(DateTime since, CancellationToken cancellationToken = default);
}
