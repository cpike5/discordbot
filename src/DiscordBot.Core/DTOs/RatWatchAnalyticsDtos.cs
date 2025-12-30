namespace DiscordBot.Core.DTOs;

/// <summary>
/// Analytics summary for Rat Watch dashboard cards.
/// </summary>
public record RatWatchAnalyticsSummaryDto
{
    /// <summary>
    /// Total number of Rat Watches in the time period.
    /// </summary>
    public int TotalWatches { get; init; }

    /// <summary>
    /// Number of Rat Watches currently active (Pending or Voting status).
    /// </summary>
    public int ActiveWatches { get; init; }

    /// <summary>
    /// Number of watches that resulted in guilty verdicts.
    /// </summary>
    public int GuiltyCount { get; init; }

    /// <summary>
    /// Number of watches where the user checked in early (cleared before voting).
    /// </summary>
    public int ClearedEarlyCount { get; init; }

    /// <summary>
    /// Percentage of completed watches that resulted in guilty verdicts.
    /// </summary>
    public double GuiltyRate { get; init; }

    /// <summary>
    /// Percentage of watches where users checked in early.
    /// </summary>
    public double EarlyCheckInRate { get; init; }

    /// <summary>
    /// Average number of total votes per watch that went to voting.
    /// </summary>
    public double AvgVotingParticipation { get; init; }

    /// <summary>
    /// Average vote margin (difference between guilty and not guilty votes).
    /// </summary>
    public double AvgVoteMargin { get; init; }
}

/// <summary>
/// Time series data point for trend charts.
/// </summary>
public record RatWatchTimeSeriesDto
{
    /// <summary>
    /// Date for this data point (day level granularity).
    /// </summary>
    public DateTime Date { get; init; }

    /// <summary>
    /// Total number of watches on this date.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Number of guilty verdicts on this date.
    /// </summary>
    public int GuiltyCount { get; init; }

    /// <summary>
    /// Number of early check-ins on this date.
    /// </summary>
    public int ClearedCount { get; init; }
}

/// <summary>
/// User metrics for leaderboard views.
/// </summary>
public record RatWatchUserMetricsDto
{
    /// <summary>
    /// Discord user snowflake ID.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Username (populated by service layer, not repository).
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Total number of watches created against this user.
    /// </summary>
    public int WatchesAgainst { get; init; }

    /// <summary>
    /// Number of guilty verdicts for this user.
    /// </summary>
    public int GuiltyCount { get; init; }

    /// <summary>
    /// Number of times this user checked in early.
    /// </summary>
    public int EarlyCheckInCount { get; init; }

    /// <summary>
    /// Accountability score: percentage of watches where user checked in early.
    /// </summary>
    public double AccountabilityScore { get; init; }

    /// <summary>
    /// Date of the most recent incident (guilty verdict or watch created).
    /// </summary>
    public DateTime? LastIncidentDate { get; init; }
}

/// <summary>
/// Fun stats container for public leaderboard.
/// </summary>
public record RatWatchFunStatsDto
{
    /// <summary>
    /// User with the longest streak of consecutive guilty verdicts.
    /// </summary>
    public UserStreakDto? LongestGuiltyStreak { get; init; }

    /// <summary>
    /// User with the longest streak of consecutive early check-ins.
    /// </summary>
    public UserStreakDto? LongestCleanStreak { get; init; }

    /// <summary>
    /// Watch with the biggest vote margin (landslide victory).
    /// </summary>
    public IncidentHighlightDto? BiggestLandslide { get; init; }

    /// <summary>
    /// Watch with the closest vote (smallest margin that still resulted in guilty).
    /// </summary>
    public IncidentHighlightDto? ClosestCall { get; init; }

    /// <summary>
    /// Watch with the fastest check-in time (from creation to early clear).
    /// </summary>
    public IncidentHighlightDto? FastestCheckIn { get; init; }

    /// <summary>
    /// Watch where the user checked in latest but still before deadline.
    /// </summary>
    public IncidentHighlightDto? LatestCheckIn { get; init; }
}

/// <summary>
/// User streak information (consecutive guilty or clean records).
/// </summary>
public record UserStreakDto
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
    /// Number of consecutive records in the streak.
    /// </summary>
    public int StreakCount { get; init; }
}

/// <summary>
/// Highlight of a specific incident for fun stats.
/// </summary>
public record IncidentHighlightDto
{
    /// <summary>
    /// Unique identifier of the Rat Watch.
    /// </summary>
    public Guid WatchId { get; init; }

    /// <summary>
    /// Discord user snowflake ID involved in the incident.
    /// </summary>
    public ulong UserId { get; init; }

    /// <summary>
    /// Username (populated by service layer).
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable description of the highlight.
    /// Examples: "10-1 Guilty", "Cleared in 23 seconds"
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Date when this incident occurred.
    /// </summary>
    public DateTime Date { get; init; }
}

/// <summary>
/// Activity heatmap data point (day of week + hour).
/// </summary>
public record ActivityHeatmapDto
{
    /// <summary>
    /// Day of the week (0=Sunday through 6=Saturday).
    /// </summary>
    public int DayOfWeek { get; init; }

    /// <summary>
    /// Hour of the day (0-23).
    /// </summary>
    public int Hour { get; init; }

    /// <summary>
    /// Number of watches scheduled at this day/hour combination.
    /// </summary>
    public int Count { get; init; }
}
