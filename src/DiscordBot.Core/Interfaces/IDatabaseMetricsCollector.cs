using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for collecting database query performance metrics.
/// Integrates with QueryPerformanceInterceptor to track query execution.
/// </summary>
public interface IDatabaseMetricsCollector
{
    /// <summary>
    /// Records the execution of a database query.
    /// </summary>
    /// <param name="durationMs">The query execution duration in milliseconds.</param>
    /// <param name="commandType">The type of command executed (e.g., "SELECT", "INSERT").</param>
    void RecordQuery(double durationMs, string commandType);

    /// <summary>
    /// Records a database query error.
    /// </summary>
    /// <param name="durationMs">The duration before the query failed in milliseconds.</param>
    /// <param name="error">The error message or exception details.</param>
    void RecordQueryError(double durationMs, string error);

    /// <summary>
    /// Records a slow database query for detailed tracking.
    /// </summary>
    /// <param name="commandText">The SQL command text.</param>
    /// <param name="durationMs">The query execution duration in milliseconds.</param>
    /// <param name="parameters">The query parameters (if captured), or null.</param>
    void RecordSlowQuery(string commandText, double durationMs, string? parameters);

    /// <summary>
    /// Gets aggregate database metrics including query counts, averages, and histogram.
    /// </summary>
    /// <returns>Database metrics summary.</returns>
    DatabaseMetricsDto GetMetrics();

    /// <summary>
    /// Gets the most recent slow queries.
    /// </summary>
    /// <param name="limit">The maximum number of slow queries to return (default: 20).</param>
    /// <returns>A read-only list of slow query details.</returns>
    IReadOnlyList<SlowQueryDto> GetSlowQueries(int limit = 20);

    /// <summary>
    /// Resets all collected metrics. Use with caution.
    /// </summary>
    void Reset();
}
