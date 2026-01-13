using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    private readonly IAuthorizationService _authorizationService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IGuildService guildService,
        IAuthorizationService authorizationService,
        UserManager<ApplicationUser> userManager,
        ILogger<IndexModel> logger)
    {
        _guildService = guildService;
        _authorizationService = authorizationService;
        _userManager = userManager;
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
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
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

    /// <summary>
    /// Indicates whether the guild list is filtered based on user access.
    /// </summary>
    public bool IsFiltered { get; set; }

    /// <summary>
    /// The total number of guilds connected to the bot (before user filtering).
    /// Only populated when IsFiltered is true.
    /// </summary>
    public int TotalGuilds { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing guild list page. Search={Search}, Status={Status}, Sort={Sort}, Page={Page}",
            SearchTerm, StatusFilter, SortBy, CurrentPage);

        // Get current user and their roles for filtering
        var user = await _userManager.GetUserAsync(User);
        var userRoles = user != null ? await _userManager.GetRolesAsync(user) : Array.Empty<string>();

        // Determine if user-based filtering will be applied
        IsFiltered = userRoles.Any() &&
                     !userRoles.Contains("SuperAdmin") &&
                     !userRoles.Contains("Admin") &&
                     (userRoles.Contains("Moderator") || userRoles.Contains("Viewer"));

        // If filtered, get total guild count before filtering
        if (IsFiltered)
        {
            var allGuilds = await _guildService.GetAllGuildsAsync(cancellationToken);
            TotalGuilds = allGuilds.Count;
        }

        var query = new GuildSearchQueryDto
        {
            SearchTerm = SearchTerm,
            IsActive = StatusFilter,
            Page = CurrentPage,
            PageSize = PageSize,
            SortBy = SortBy,
            SortDescending = SortDescending,
            UserId = user?.Id,
            UserRoles = userRoles
        };

        var paginatedGuilds = await _guildService.GetGuildsAsync(query, cancellationToken);

        ViewModel = GuildListViewModel.FromPaginatedDto(paginatedGuilds, query);

        return Page();
    }

    /// <summary>
    /// Handles POST request to sync a single guild from Discord.
    /// </summary>
    public async Task<IActionResult> OnPostSyncGuildAsync(ulong id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User requesting sync for guild {GuildId}", id);

        try
        {
            var success = await _guildService.SyncGuildAsync(id, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Successfully synced guild {GuildId}", id);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = true, message = "Guild synced successfully" });
                }

                return RedirectToPage();
            }
            else
            {
                _logger.LogWarning("Failed to sync guild {GuildId} - guild not found in Discord", id);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = false, message = "Guild not found in Discord client" });
                }

                return RedirectToPage();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing guild {GuildId}", id);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { success = false, message = "An error occurred while syncing the guild" });
            }

            return RedirectToPage();
        }
    }

    /// <summary>
    /// Handles POST request to sync all guilds from Discord (Admin only).
    /// </summary>
    public async Task<IActionResult> OnPostSyncAllAsync(CancellationToken cancellationToken)
    {
        // Manually check Admin authorization since attributes can't be applied to Razor Page handlers
        var authResult = await _authorizationService.AuthorizeAsync(User, "RequireAdmin");
        if (!authResult.Succeeded)
        {
            _logger.LogWarning("User {User} attempted to sync all guilds without admin privileges", User.Identity?.Name);
            return Forbid();
        }

        _logger.LogInformation("User requesting sync for all guilds");

        try
        {
            var syncedCount = await _guildService.SyncAllGuildsAsync(cancellationToken);

            _logger.LogInformation("Successfully synced {SyncedCount} guilds", syncedCount);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new
                {
                    success = true,
                    syncedCount = syncedCount,
                    message = $"Successfully synced {syncedCount} guild{(syncedCount != 1 ? "s" : "")}"
                });
            }

            return RedirectToPage();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing all guilds");

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { success = false, message = "An error occurred while syncing guilds" });
            }

            return RedirectToPage();
        }
    }
}
