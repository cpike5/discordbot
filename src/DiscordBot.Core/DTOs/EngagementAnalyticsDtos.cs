namespace DiscordBot.Core.DTOs;

/// <summary>
/// Summary metrics for engagement analytics dashboard.
/// </summary>
public record EngagementAnalyticsSummaryDto
{
    /// <summary>
    /// Total number of messages in the time period.
    /// </summary>
    public long TotalMessages { get; init; }

    /// <summary>
    /// Number of messages sent in the last 24 hours.
    /// </summary>
    public long Messages24h { get; init; }

    /// <summary>
    /// Number of messages sent in the last 7 days.
    /// </summary>
    public long Messages7d { get; init; }

    /// <summary>
    /// Average number of messages per day in the time period.
    /// </summary>
    public decimal MessagesPerDay { get; init; }

    /// <summary>
    /// Number of members who have sent at least one message in the time period.
    /// </summary>
    public int ActiveMembers { get; init; }

    /// <summary>
    /// Number of members who joined in the last 7 days.
    /// </summary>
    public int NewMembers7d { get; init; }

    /// <summary>
    /// Percentage of new members who sent at least one message within 7 days of joining.
    /// </summary>
    public decimal NewMemberRetentionRate { get; init; }

    /// <summary>
    /// Total number of reactions added to messages (placeholder - not currently tracked).
    /// </summary>
    public long ReactionCount { get; init; }

    /// <summary>
    /// Total minutes spent in voice channels (placeholder - not currently tracked).
    /// </summary>
    public long VoiceMinutes { get; init; }
}

/// <summary>
/// Time series data point for message trends over time.
/// </summary>
public record MessageTrendDto
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
    /// Number of unique authors who posted messages on this date.
    /// </summary>
    public int UniqueAuthors { get; init; }

    /// <summary>
    /// Average message length (in characters) on this date.
    /// </summary>
    public decimal AvgMessageLength { get; init; }
}

/// <summary>
/// Retention metrics for new members showing engagement patterns.
/// </summary>
public record NewMemberRetentionDto
{
    /// <summary>
    /// Date when members joined (grouped by day).
    /// </summary>
    public DateTime JoinDate { get; init; }

    /// <summary>
    /// Number of members who joined on this date.
    /// </summary>
    public int NewMembers { get; init; }

    /// <summary>
    /// Number of new members who sent their first message within 24 hours.
    /// </summary>
    public int SentFirstMessage { get; init; }

    /// <summary>
    /// Number of new members still active 7 days after joining.
    /// </summary>
    public int StillActive7d { get; init; }

    /// <summary>
    /// Number of new members still active 30 days after joining.
    /// </summary>
    public int StillActive30d { get; init; }

    /// <summary>
    /// Percentage of new members who sent their first message within 24 hours.
    /// </summary>
    public decimal FirstMessageRate { get; init; }

    /// <summary>
    /// Percentage of new members still active after 7 days.
    /// </summary>
    public decimal Retention7dRate { get; init; }

    /// <summary>
    /// Percentage of new members still active after 30 days.
    /// </summary>
    public decimal Retention30dRate { get; init; }
}
