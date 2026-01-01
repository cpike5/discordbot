using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for generating moderation analytics and metrics.
/// </summary>
public interface IModerationAnalyticsService
{
    /// <summary>
    /// Gets summary metrics for moderation activity including case counts and trends.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="start">Start of the time period (inclusive).</param>
    /// <param name="end">End of the time period (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Summary metrics for the specified time period.</returns>
    Task<ModerationAnalyticsSummaryDto> GetSummaryAsync(ulong guildId, DateTime start, DateTime end, CancellationToken ct = default);

    /// <summary>
    /// Gets time series data for moderation trends showing case counts by type over time.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="start">Start of the time period (inclusive).</param>
    /// <param name="end">End of the time period (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Daily moderation trend data points for the specified time period.</returns>
    Task<IReadOnlyList<ModerationTrendDto>> GetTrendsAsync(ulong guildId, DateTime start, DateTime end, CancellationToken ct = default);

    /// <summary>
    /// Gets repeat offenders with multiple moderation cases, showing escalation patterns.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="start">Start of the time period (inclusive).</param>
    /// <param name="end">End of the time period (inclusive).</param>
    /// <param name="limit">Maximum number of offenders to return (default: 10).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Top repeat offenders with case counts and escalation history.</returns>
    Task<IReadOnlyList<RepeatOffenderDto>> GetRepeatOffendersAsync(ulong guildId, DateTime start, DateTime end, int limit = 10, CancellationToken ct = default);

    /// <summary>
    /// Gets moderator workload distribution showing action counts by moderator.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="start">Start of the time period (inclusive).</param>
    /// <param name="end">End of the time period (inclusive).</param>
    /// <param name="limit">Maximum number of moderators to return (default: 10).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Moderator workload metrics showing action distribution.</returns>
    Task<IReadOnlyList<ModeratorWorkloadDto>> GetModeratorWorkloadAsync(ulong guildId, DateTime start, DateTime end, int limit = 10, CancellationToken ct = default);

    /// <summary>
    /// Gets distribution of moderation case types for the specified time period.
    /// </summary>
    /// <param name="guildId">Discord guild snowflake ID.</param>
    /// <param name="start">Start of the time period (inclusive).</param>
    /// <param name="end">End of the time period (inclusive).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Case type distribution metrics.</returns>
    Task<CaseTypeDistributionDto> GetCaseDistributionAsync(ulong guildId, DateTime start, DateTime end, CancellationToken ct = default);
}
