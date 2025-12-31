namespace DiscordBot.Core.DTOs;

/// <summary>
/// Query parameters for searching, filtering, and paginating guild members.
/// </summary>
public class GuildMemberQueryDto
{
    /// <summary>
    /// Search term to filter by username, global display name, or nickname.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by role IDs (multi-select). Members must have ALL specified roles.
    /// </summary>
    public List<ulong>? RoleIds { get; set; }

    /// <summary>
    /// Filter by join date range start (inclusive).
    /// </summary>
    public DateTime? JoinedAtStart { get; set; }

    /// <summary>
    /// Filter by join date range end (inclusive).
    /// </summary>
    public DateTime? JoinedAtEnd { get; set; }

    /// <summary>
    /// Filter by last active date range start (inclusive).
    /// </summary>
    public DateTime? LastActiveAtStart { get; set; }

    /// <summary>
    /// Filter by last active date range end (inclusive).
    /// </summary>
    public DateTime? LastActiveAtEnd { get; set; }

    /// <summary>
    /// Filter by active status. Null for all, true for active only, false for inactive only.
    /// </summary>
    public bool? IsActive { get; set; } = true;

    /// <summary>
    /// Page number (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; set; } = 25;

    /// <summary>
    /// Field to sort by: Username, DisplayName, JoinedAt, or LastActiveAt.
    /// </summary>
    public string SortBy { get; set; } = "JoinedAt";

    /// <summary>
    /// Sort in descending order if true.
    /// </summary>
    public bool SortDescending { get; set; }

    /// <summary>
    /// Filter by specific user IDs. Used for exporting selected members.
    /// </summary>
    public List<ulong>? UserIds { get; set; }
}
