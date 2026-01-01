using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for generating server activity analytics and metrics.
/// </summary>
public interface IServerAnalyticsService
{
    /// <summary>
    /// Gets summary metrics for server activity including member counts, message volume, and growth.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="start">Start of the time period (inclusive).</param>
    /// <param name="end">End of the time period (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary metrics for the specified time period.</returns>
    Task<ServerAnalyticsSummaryDto> GetSummaryAsync(ulong guildId, DateTime start, DateTime end, CancellationToken ct = default);

    /// <summary>
    /// Gets time series data for activity trends (messages, active members, active channels) over time.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="start">Start of the time period (inclusive).</param>
    /// <param name="end">End of the time period (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Daily activity data points for the specified time period.</returns>
    Task<IReadOnlyList<ActivityTimeSeriesDto>> GetActivityTimeSeriesAsync(ulong guildId, DateTime start, DateTime end, CancellationToken ct = default);

    /// <summary>
    /// Gets heatmap data showing message activity by day of week and hour.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="start">Start of the time period (inclusive).</param>
    /// <param name="end">End of the time period (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Heatmap data points (day/hour combinations with message counts).</returns>
    Task<IReadOnlyList<ServerActivityHeatmapDto>> GetActivityHeatmapAsync(ulong guildId, DateTime start, DateTime end, CancellationToken ct = default);

    /// <summary>
    /// Gets top contributors ranked by message count in the specified time period.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="start">Start of the time period (inclusive).</param>
    /// <param name="end">End of the time period (inclusive).</param>
    /// <param name="limit">Maximum number of contributors to return (default: 10).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Top contributors with message counts and activity metrics.</returns>
    Task<IReadOnlyList<TopContributorDto>> GetTopContributorsAsync(ulong guildId, DateTime start, DateTime end, int limit = 10, CancellationToken ct = default);
}
