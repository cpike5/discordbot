using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for AssistantUsageMetrics entities.
/// Provides data access operations for assistant usage tracking, token consumption, and cost analytics.
/// </summary>
public interface IAssistantUsageMetricsRepository : IRepository<AssistantUsageMetrics>
{
    /// <summary>
    /// Gets or creates metrics for a guild on a specific date.
    /// If no metrics exist for the date, creates a new entry with default values.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="date">Date to retrieve or create metrics for (UTC, date-only).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The existing or newly created metrics entry.</returns>
    Task<AssistantUsageMetrics> GetOrCreateAsync(
        ulong guildId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metrics for a guild over a date range.
    /// Returns one entry per day where data exists.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="startDate">Start date of the range (inclusive, UTC).</param>
    /// <param name="endDate">End date of the range (inclusive, UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of metrics ordered by date ascending.</returns>
    Task<IEnumerable<AssistantUsageMetrics>> GetRangeAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments metrics for a successful interaction.
    /// Updates token counts, cache statistics, latency, and cost for the specified date.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="date">Date to update metrics for (UTC, date-only).</param>
    /// <param name="inputTokens">Number of input tokens consumed.</param>
    /// <param name="outputTokens">Number of output tokens generated.</param>
    /// <param name="cachedTokens">Number of cache read tokens (prompt cache hit).</param>
    /// <param name="cacheWriteTokens">Number of cache write tokens (prompt cache creation).</param>
    /// <param name="cacheHit">Whether the interaction resulted in a cache hit.</param>
    /// <param name="toolCalls">Number of tool calls made during the interaction.</param>
    /// <param name="latencyMs">Response latency in milliseconds.</param>
    /// <param name="cost">Estimated cost in USD for the interaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task IncrementMetricsAsync(
        ulong guildId,
        DateTime date,
        int inputTokens,
        int outputTokens,
        int cachedTokens,
        int cacheWriteTokens,
        bool cacheHit,
        int toolCalls,
        int latencyMs,
        decimal cost,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increments failed request count.
    /// Used when API calls fail due to errors, rate limits, or timeouts.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="date">Date to update metrics for (UTC, date-only).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task IncrementFailedRequestAsync(
        ulong guildId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes metrics older than the specified date.
    /// Used for retention policy enforcement and database cleanup.
    /// </summary>
    /// <param name="cutoffDate">The cutoff date. Entries with Date &lt; cutoffDate will be deleted.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of entries deleted.</returns>
    Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate,
        CancellationToken cancellationToken = default);
}
