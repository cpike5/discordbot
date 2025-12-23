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
}
