using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages;

/// <summary>
/// Abstract base class for pages that support pagination and optional sorting.
/// Provides common [BindProperty] declarations so individual pages don't need to redeclare them.
/// </summary>
public abstract class PaginatedPageModel : PageModel
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
