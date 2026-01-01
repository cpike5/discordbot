using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for generating engagement analytics and member retention metrics.
/// </summary>
public interface IEngagementAnalyticsService
{
    /// <summary>
    /// Gets summary metrics for engagement including message volume, active members, and retention.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="start">Start of the time period (inclusive).</param>
    /// <param name="end">End of the time period (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary metrics for the specified time period.</returns>
    Task<EngagementAnalyticsSummaryDto> GetSummaryAsync(ulong guildId, DateTime start, DateTime end, CancellationToken ct = default);

    /// <summary>
    /// Gets time series data for message trends including counts, unique authors, and average length.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="start">Start of the time period (inclusive).</param>
    /// <param name="end">End of the time period (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Daily message trend data points for the specified time period.</returns>
    Task<IReadOnlyList<MessageTrendDto>> GetMessageTrendsAsync(ulong guildId, DateTime start, DateTime end, CancellationToken ct = default);

    /// <summary>
    /// Gets new member retention metrics showing engagement patterns after joining.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="start">Start of the time period (inclusive).</param>
    /// <param name="end">End of the time period (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Daily retention metrics for members who joined in the time period.</returns>
    Task<IReadOnlyList<NewMemberRetentionDto>> GetNewMemberRetentionAsync(ulong guildId, DateTime start, DateTime end, CancellationToken ct = default);
}
