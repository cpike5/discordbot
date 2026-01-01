using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for aggregating command performance metrics from command logs.
/// Runs as a background service with caching to avoid expensive recalculations.
/// </summary>
public interface ICommandPerformanceAggregator
{
    /// <summary>
    /// Gets aggregated performance metrics for all commands over a specified number of hours.
    /// Results are cached according to the configured TTL.
    /// </summary>
    /// <param name="hours">The number of hours of command history to aggregate (default: 24).</param>
    /// <returns>A read-only list of command performance aggregates.</returns>
    Task<IReadOnlyList<CommandPerformanceAggregateDto>> GetAggregatesAsync(int hours = 24);

    /// <summary>
    /// Gets the slowest command executions over a specified number of hours.
    /// </summary>
    /// <param name="limit">The maximum number of results to return (default: 10).</param>
    /// <param name="hours">The number of hours of command history to query (default: 24).</param>
    /// <returns>A read-only list of the slowest commands.</returns>
    Task<IReadOnlyList<SlowestCommandDto>> GetSlowestCommandsAsync(int limit = 10, int hours = 24);

    /// <summary>
    /// Gets command execution throughput over time with configurable granularity.
    /// </summary>
    /// <param name="hours">The number of hours of history to include (default: 24).</param>
    /// <param name="granularity">The time bucket granularity: "hour" or "day" (default: "hour").</param>
    /// <returns>A read-only list of throughput measurements over time.</returns>
    Task<IReadOnlyList<CommandThroughputDto>> GetThroughputAsync(int hours = 24, string granularity = "hour");

    /// <summary>
    /// Gets error breakdown by command over a specified number of hours.
    /// </summary>
    /// <param name="hours">The number of hours of command history to analyze (default: 24).</param>
    /// <param name="limit">The maximum number of commands to return (default: 50).</param>
    /// <returns>A read-only list of error breakdowns by command.</returns>
    Task<IReadOnlyList<CommandErrorBreakdownDto>> GetErrorBreakdownAsync(int hours = 24, int limit = 50);

    /// <summary>
    /// Invalidates the cached aggregation data, forcing a recalculation on the next request.
    /// </summary>
    void InvalidateCache();
}
