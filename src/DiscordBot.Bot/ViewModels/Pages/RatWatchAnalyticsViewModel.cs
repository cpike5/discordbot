using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for the Guild Rat Watch Analytics page.
/// </summary>
public record RatWatchAnalyticsViewModel
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
    /// Analytics summary with counts, rates, and averages.
    /// </summary>
    public RatWatchAnalyticsSummaryDto Summary { get; init; } = new();

    /// <summary>
    /// Time series data for watches over time chart.
    /// </summary>
    public IReadOnlyList<RatWatchTimeSeriesDto> TimeSeries { get; init; } = Array.Empty<RatWatchTimeSeriesDto>();

    /// <summary>
    /// Activity heatmap data (day of week + hour).
    /// </summary>
    public IReadOnlyList<ActivityHeatmapDto> Heatmap { get; init; } = Array.Empty<ActivityHeatmapDto>();

    /// <summary>
    /// Top 10 most watched users leaderboard.
    /// </summary>
    public IReadOnlyList<RatWatchUserMetricsDto> MostWatched { get; init; } = Array.Empty<RatWatchUserMetricsDto>();

    /// <summary>
    /// Top 10 accusers (users who created the most watches).
    /// </summary>
    public IReadOnlyList<AccuserMetricsDto> TopAccusers { get; init; } = Array.Empty<AccuserMetricsDto>();

    /// <summary>
    /// Top 10 users with the most guilty verdicts.
    /// </summary>
    public IReadOnlyList<RatWatchUserMetricsDto> BiggestRats { get; init; } = Array.Empty<RatWatchUserMetricsDto>();

    /// <summary>
    /// Start date for filtering (UTC).
    /// </summary>
    public DateTime StartDate { get; init; }

    /// <summary>
    /// End date for filtering (UTC).
    /// </summary>
    public DateTime EndDate { get; init; }
}

/// <summary>
/// Metrics for users who create watches (accusers).
/// </summary>
public record AccuserMetricsDto
{
    /// <summary>
    /// Discord user snowflake ID.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Username (populated by service layer).
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Total number of watches created by this user.
    /// </summary>
    public int WatchesCreated { get; init; }

    /// <summary>
    /// Number of watches that resulted in guilty verdicts.
    /// </summary>
    public int GuiltyCount { get; init; }

    /// <summary>
    /// Success rate: percentage of created watches that resulted in guilty verdicts.
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Date of the most recently created watch.
    /// </summary>
    public DateTime? LastCreatedDate { get; init; }
}
