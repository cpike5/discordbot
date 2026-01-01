using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Guild Moderation Analytics page.
/// </summary>
public record ModerationAnalyticsViewModel
{
    /// <summary>
    /// The guild's Discord snowflake ID.
    /// </summary>
    public ulong GuildId { get; init; }

    /// <summary>
    /// The guild's name.
    /// </summary>
    public string GuildName { get; init; } = string.Empty;

    /// <summary>
    /// Optional guild icon URL.
    /// </summary>
    public string? GuildIconUrl { get; init; }

    /// <summary>
    /// Summary metrics with total counts, rates, and averages.
    /// </summary>
    public ModerationAnalyticsSummaryDto Summary { get; init; } = new();

    /// <summary>
    /// Time series data for moderation trends over time chart.
    /// </summary>
    public IReadOnlyList<ModerationTrendDto> Trends { get; init; } = Array.Empty<ModerationTrendDto>();

    /// <summary>
    /// Distribution of case types for doughnut chart.
    /// </summary>
    public CaseTypeDistributionDto Distribution { get; init; } = new();

    /// <summary>
    /// Repeat offenders with multiple moderation cases.
    /// </summary>
    public IReadOnlyList<RepeatOffenderDto> RepeatOffenders { get; init; } = Array.Empty<RepeatOffenderDto>();

    /// <summary>
    /// Moderator workload distribution showing cases handled per moderator.
    /// </summary>
    public IReadOnlyList<ModeratorWorkloadDto> ModeratorWorkload { get; init; } = Array.Empty<ModeratorWorkloadDto>();

    /// <summary>
    /// Start date for filtering (UTC).
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// End date for filtering (UTC).
    /// </summary>
    public DateTime EndDate { get; init; }
}
