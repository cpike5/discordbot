using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Filter parameters for querying Rat Watch incidents.
/// </summary>
public class RatWatchIncidentFilterDto
{
    /// <summary>
    /// Filter by one or more status values. If null/empty, includes all statuses.
    /// </summary>
    public IReadOnlyList<RatWatchStatus>? Statuses { get; set; }

    /// <summary>
    /// Filter by start of date range (UTC). Filters on ScheduledAt.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Filter by end of date range (UTC). Filters on ScheduledAt.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Filter by accused user. Supports Discord ID (numeric) or partial username match.
    /// </summary>
    public string? AccusedUser { get; set; }

    /// <summary>
    /// Filter by initiator user. Supports Discord ID (numeric) or partial username match.
    /// </summary>
    public string? InitiatorUser { get; set; }

    /// <summary>
    /// Minimum total votes (guilty + not guilty) threshold.
    /// </summary>
    public int? MinVoteCount { get; set; }

    /// <summary>
    /// Keyword search in CustomMessage (case-insensitive).
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// Page number (1-based). Default is 1.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Page size. Default is 25.
    /// </summary>
    public int PageSize { get; set; } = 25;

    /// <summary>
    /// Sort column. Default is "ScheduledAt".
    /// </summary>
    public string SortBy { get; set; } = "ScheduledAt";

    /// <summary>
    /// Sort descending. Default is true (newest first).
    /// </summary>
    public bool SortDescending { get; set; } = true;
}
