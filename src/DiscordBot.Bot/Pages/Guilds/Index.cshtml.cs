using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for displaying the guild list with search, filter, sort, and pagination.
/// </summary>
[Authorize(Policy = "RequireModerator")]
public class IndexModel : PageModel
{
    private readonly IGuildService _guildService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IGuildService guildService, ILogger<IndexModel> logger)
    {
        _guildService = guildService;
        _logger = logger;
    }

    /// <summary>
    /// Search term for filtering guilds by name or ID.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by active status (null for all, true for active, false for inactive).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public bool? StatusFilter { get; set; }

    /// <summary>
    /// Field to sort by (Name, MemberCount, JoinedAt).
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
    [BindProperty(SupportsGet = true, Name = "page")]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Number of items per page.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// The view model containing guild list data.
    /// </summary>
    public GuildListViewModel ViewModel { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing guild list page. Search={Search}, Status={Status}, Sort={Sort}, Page={Page}",
            SearchTerm, StatusFilter, SortBy, CurrentPage);

        var query = new GuildSearchQueryDto
        {
            SearchTerm = SearchTerm,
            IsActive = StatusFilter,
            Page = CurrentPage,
            PageSize = PageSize,
            SortBy = SortBy,
            SortDescending = SortDescending
        };

        var paginatedGuilds = await _guildService.GetGuildsAsync(query, cancellationToken);

        ViewModel = GuildListViewModel.FromPaginatedDto(paginatedGuilds, query);

        return Page();
    }
}
