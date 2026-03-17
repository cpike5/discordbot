using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Abstract base class for guild-scoped pages that support pagination and optional sorting.
/// Extends <see cref="GuildPageModelBase"/> with common [BindProperty] pagination declarations.
/// </summary>
public abstract class PaginatedGuildPageModel : GuildPageModelBase
{
    /// <summary>
    /// Field to sort by. Subclasses should override the default in their constructor if needed.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "Name";

    /// <summary>
    /// Sort in descending order if true.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public bool SortDescending { get; set; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Number of items per page. Subclasses should override the default in their constructor if needed.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Total number of pages based on the current query.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Total number of items matching the current query.
    /// </summary>
    public int TotalCount { get; set; }
}
