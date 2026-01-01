namespace DiscordBot.Core.DTOs;

/// <summary>
/// Summary metrics for moderation analytics dashboard.
/// </summary>
public record ModerationAnalyticsSummaryDto
{
    /// <summary>
    /// Total number of moderation cases in the time period.
    /// </summary>
    public int TotalCases { get; init; }

    /// <summary>
    /// Number of warning cases.
    /// </summary>
    public int WarnCount { get; init; }

    /// <summary>
    /// Number of mute/timeout cases.
    /// </summary>
    public int MuteCount { get; init; }

    /// <summary>
    /// Number of kick cases.
    /// </summary>
    public int KickCount { get; init; }

    /// <summary>
    /// Number of ban cases.
    /// </summary>
    public int BanCount { get; init; }

    /// <summary>
    /// Number of note-only cases (no punitive action).
    /// </summary>
    public int NoteCount { get; init; }

    /// <summary>
    /// Number of moderation cases in the last 24 hours.
    /// </summary>
    public int Cases24h { get; init; }

    /// <summary>
    /// Number of moderation cases in the last 7 days.
    /// </summary>
    public int Cases7d { get; init; }

    /// <summary>
    /// Average number of cases per day in the time period.
    /// </summary>
    public decimal CasesPerDay { get; init; }

    /// <summary>
    /// Percentage change in case volume compared to the previous period of equal length.
    /// </summary>
    public decimal ChangeFromPreviousPeriod { get; init; }
}

/// <summary>
/// Time series data point for moderation trends over time.
/// </summary>
public record ModerationTrendDto
{
    /// <summary>
    /// Date for this data point (day-level granularity).
    /// </summary>
    public DateTime Date { get; init; }

    /// <summary>
    /// Total number of moderation cases on this date.
    /// </summary>
    public int TotalCases { get; init; }

    /// <summary>
    /// Number of warnings on this date.
    /// </summary>
    public int WarnCount { get; init; }

    /// <summary>
    /// Number of mutes on this date.
    /// </summary>
    public int MuteCount { get; init; }

    /// <summary>
    /// Number of kicks on this date.
    /// </summary>
    public int KickCount { get; init; }

    /// <summary>
    /// Number of bans on this date.
    /// </summary>
    public int BanCount { get; init; }
}

/// <summary>
/// Metrics for users with multiple moderation cases (repeat offenders).
/// </summary>
public record RepeatOffenderDto
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
    /// URL to user's Discord avatar.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Total number of moderation cases for this user.
    /// </summary>
    public int TotalCases { get; init; }

    /// <summary>
    /// Number of warnings received.
    /// </summary>
    public int WarnCount { get; init; }

    /// <summary>
    /// Number of mutes received.
    /// </summary>
    public int MuteCount { get; init; }

    /// <summary>
    /// Number of kicks received.
    /// </summary>
    public int KickCount { get; init; }

    /// <summary>
    /// Number of bans received.
    /// </summary>
    public int BanCount { get; init; }

    /// <summary>
    /// Date of the user's first moderation case.
    /// </summary>
    public DateTime FirstIncident { get; init; }

    /// <summary>
    /// Date of the user's most recent moderation case.
    /// </summary>
    public DateTime LastIncident { get; init; }

    /// <summary>
    /// Escalation path showing progression of punishments (e.g., ["Warn", "Mute", "Kick", "Ban"]).
    /// </summary>
    public IReadOnlyList<string> EscalationPath { get; init; } = [];
}

/// <summary>
/// Workload metrics for moderators showing action distribution.
/// </summary>
public record ModeratorWorkloadDto
{
    /// <summary>
    /// Discord user snowflake ID of the moderator.
    /// </summary>
    public ulong ModeratorId { get; init; }

    /// <summary>
    /// Moderator username (populated by service layer).
    /// </summary>
    public string ModeratorUsername { get; init; } = string.Empty;

    /// <summary>
    /// URL to moderator's Discord avatar.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Total number of moderation actions performed.
    /// </summary>
    public int TotalActions { get; init; }

    /// <summary>
    /// Number of warnings issued.
    /// </summary>
    public int WarnCount { get; init; }

    /// <summary>
    /// Number of mutes issued.
    /// </summary>
    public int MuteCount { get; init; }

    /// <summary>
    /// Number of kicks performed.
    /// </summary>
    public int KickCount { get; init; }

    /// <summary>
    /// Number of bans issued.
    /// </summary>
    public int BanCount { get; init; }

    /// <summary>
    /// Percentage of total moderation actions performed by this moderator.
    /// </summary>
    public decimal Percentage { get; init; }
}

/// <summary>
/// Distribution of moderation case types for pie chart display.
/// </summary>
public record CaseTypeDistributionDto
{
    /// <summary>
    /// Number of warning cases.
    /// </summary>
    public int WarnCount { get; init; }

    /// <summary>
    /// Number of mute cases.
    /// </summary>
    public int MuteCount { get; init; }

    /// <summary>
    /// Number of kick cases.
    /// </summary>
    public int KickCount { get; init; }

    /// <summary>
    /// Number of ban cases.
    /// </summary>
    public int BanCount { get; init; }

    /// <summary>
    /// Number of note-only cases.
    /// </summary>
    public int NoteCount { get; init; }

    /// <summary>
    /// Total number of all cases.
    /// </summary>
    public int Total { get; init; }
}
