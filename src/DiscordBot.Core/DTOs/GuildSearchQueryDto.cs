namespace DiscordBot.Core.DTOs;

/// <summary>
/// Query parameters for searching and paginating guilds.
/// </summary>
public class GuildSearchQueryDto
{
    /// <summary>
    /// Search term to filter by guild name or ID.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by active status. Null for all, true for active only, false for inactive only.
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Page number (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Number of items per page.
    /// </summary>
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Field to sort by: Name, MemberCount, or JoinedAt.
    /// </summary>
    public string SortBy { get; set; } = "Name";

    /// <summary>
    /// Sort in descending order if true.
    /// </summary>
    public bool SortDescending { get; set; }

    /// <summary>
    /// The requesting user's ID for filtering guilds based on access rights.
    /// If null, no user-based filtering is applied (SuperAdmin/Admin see all).
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The requesting user's roles for determining filtering behavior.
    /// SuperAdmin and Admin see all guilds. Moderator and Viewer see only guilds they're Discord members of.
    /// </summary>
    public IEnumerable<string>? UserRoles { get; set; }
}
