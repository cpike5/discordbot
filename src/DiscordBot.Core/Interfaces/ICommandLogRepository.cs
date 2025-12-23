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
}
