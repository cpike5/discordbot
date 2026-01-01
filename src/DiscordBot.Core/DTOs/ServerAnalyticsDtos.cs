namespace DiscordBot.Core.DTOs;

/// <summary>
/// Summary metrics for server activity analytics dashboard.
/// </summary>
public record ServerAnalyticsSummaryDto
{
    /// <summary>
    /// Total number of members in the guild.
    /// </summary>
    public int TotalMembers { get; init; }

    /// <summary>
    /// Number of members currently online.
    /// </summary>
    public int OnlineMembers { get; init; }

    /// <summary>
    /// Number of members who posted messages in the last 24 hours.
    /// </summary>
    public int ActiveMembers24h { get; init; }

    /// <summary>
    /// Number of members who posted messages in the last 7 days.
    /// </summary>
    public int ActiveMembers7d { get; init; }

    /// <summary>
    /// Number of members who posted messages in the last 30 days.
    /// </summary>
    public int ActiveMembers30d { get; init; }

    /// <summary>
    /// Total messages sent in the last 24 hours.
    /// </summary>
    public long Messages24h { get; init; }

    /// <summary>
    /// Total messages sent in the last 7 days.
    /// </summary>
    public long Messages7d { get; init; }

    /// <summary>
    /// Net change in member count over the last 7 days (positive or negative).
    /// </summary>
    public int MemberGrowth7d { get; init; }

    /// <summary>
    /// Percentage change in member count over the last 7 days.
    /// </summary>
    public decimal MemberGrowthPercent { get; init; }

    /// <summary>
    /// Number of channels with at least one message in the time period.
    /// </summary>
    public int ActiveChannels { get; init; }
}

/// <summary>
/// Time series data point for activity trends over time.
/// </summary>
public record ActivityTimeSeriesDto
{
    /// <summary>
    /// Date for this data point (day-level granularity).
    /// </summary>
    public DateTime Date { get; init; }

    /// <summary>
    /// Total number of messages on this date.
    /// </summary>
    public long MessageCount { get; init; }

    /// <summary>
    /// Number of unique members who posted on this date.
    /// </summary>
    public int ActiveMembers { get; init; }

    /// <summary>
    /// Number of unique channels with messages on this date.
    /// </summary>
    public int ActiveChannels { get; init; }
}

/// <summary>
/// Heatmap data point showing message activity by day of week and hour.
/// </summary>
public record ServerActivityHeatmapDto
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
    /// Number of messages sent during this day/hour combination.
    /// </summary>
    public long MessageCount { get; init; }
}

/// <summary>
/// Top contributor metrics for leaderboard display.
/// </summary>
public record TopContributorDto
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
    /// Display name or nickname (if set).
    /// </summary>
    public string? DisplayName { get; init; }

    /// <summary>
    /// URL to user's Discord avatar.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Total number of messages sent by this user in the time period.
    /// </summary>
    public long MessageCount { get; init; }

    /// <summary>
    /// Number of unique channels this user posted in.
    /// </summary>
    public int UniqueChannels { get; init; }

    /// <summary>
    /// Timestamp of the user's most recent message.
    /// </summary>
    public DateTime LastActive { get; init; }
}
