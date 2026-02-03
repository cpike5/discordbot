using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.Vox;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.Vox;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Pages.Guilds.VOX;

/// <summary>
/// Page model for the VOX management page.
/// Displays VOX clip library browser and settings for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : PageModel
{
    private readonly IVoxClipLibrary _voxClipLibrary;
    private readonly IGuildService _guildService;
    private readonly VoxOptions _voxOptions;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IVoxClipLibrary voxClipLibrary,
        IGuildService guildService,
        IOptions<VoxOptions> voxOptions,
        ILogger<IndexModel> logger)
    {
        _voxClipLibrary = voxClipLibrary;
        _guildService = guildService;
        _voxOptions = voxOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Guild layout breadcrumb ViewModel.
    /// </summary>
    public GuildBreadcrumbViewModel Breadcrumb { get; set; } = new();

    /// <summary>
    /// Guild layout header ViewModel.
    /// </summary>
    public GuildHeaderViewModel Header { get; set; } = new();

    /// <summary>
    /// Guild layout navigation ViewModel.
    /// </summary>
    public GuildNavBarViewModel Navigation { get; set; } = new();

    /// <summary>
    /// Gets or sets the guild ID from the route.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the guild name for display.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guild icon URL for display.
    /// </summary>
    public string? GuildIconUrl { get; set; }

    /// <summary>
    /// Current settings from VoxOptions.
    /// </summary>
    public VoxOptions Settings { get; set; } = new();

    /// <summary>
    /// Current selected group filter.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string GroupFilter { get; set; } = "all";

    /// <summary>
    /// Current search query.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? SearchQuery { get; set; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    /// <summary>
    /// Clips per page.
    /// </summary>
    public const int PageSize = 50;

    /// <summary>
    /// Filtered clips to display.
    /// </summary>
    public List<VoxClipInfo> FilteredClips { get; set; } = new();

    /// <summary>
    /// Total clip count for current filter.
    /// </summary>
    public int TotalClipCount { get; set; }

    /// <summary>
    /// Total pages for pagination.
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// Clip counts by group.
    /// </summary>
    public Dictionary<VoxClipGroup, int> ClipCountsByGroup { get; set; } = new();

    /// <summary>
    /// Success message from TempData.
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Error message from TempData.
    /// </summary>
    [TempData]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Handles GET requests to display the VOX management page.
    /// </summary>
    /// <param name="guildId">The guild ID from the route.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(long guildId, CancellationToken cancellationToken = default)
    {
        GuildId = (ulong)guildId;
        _logger.LogInformation("User accessing VOX management for guild {GuildId}", GuildId);

        try
        {
            // Get guild info from service
            var guild = await _guildService.GetGuildByIdAsync(GuildId, cancellationToken);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found", GuildId);
                return NotFound();
            }

            GuildName = guild.Name;
            GuildIconUrl = guild.IconUrl;
            Settings = _voxOptions;

            // Cache enum values to avoid multiple iterations
            var groups = Enum.GetValues<VoxClipGroup>();

            // Get clip counts by group
            foreach (VoxClipGroup group in groups)
            {
                ClipCountsByGroup[group] = _voxClipLibrary.GetClipCount(group);
            }

            // Get and filter clips
            var allClips = new List<VoxClipInfo>();

            if (GroupFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                // Get clips from all groups
                foreach (VoxClipGroup group in groups)
                {
                    allClips.AddRange(_voxClipLibrary.GetClips(group));
                }
            }
            else if (Enum.TryParse<VoxClipGroup>(GroupFilter, true, out var selectedGroup))
            {
                // Get clips from selected group only
                allClips.AddRange(_voxClipLibrary.GetClips(selectedGroup));
            }
            else
            {
                // Invalid filter, default to all
                foreach (VoxClipGroup group in groups)
                {
                    allClips.AddRange(_voxClipLibrary.GetClips(group));
                }
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                allClips = allClips
                    .Where(c => c.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            // Sort by name
            allClips = allClips.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList();

            TotalClipCount = allClips.Count;
            TotalPages = (int)Math.Ceiling(TotalClipCount / (double)PageSize);

            // Ensure page is within bounds
            if (PageNumber < 1) PageNumber = 1;
            if (PageNumber > TotalPages && TotalPages > 0) PageNumber = TotalPages;

            // Apply pagination
            FilteredClips = allClips
                .Skip((PageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            _logger.LogDebug("Retrieved {Count} VOX clips for guild {GuildId}, page {PageNumber}/{TotalPages}",
                FilteredClips.Count, GuildId, PageNumber, TotalPages);

            // Populate guild layout ViewModels
            Breadcrumb = new GuildBreadcrumbViewModel
            {
                Items = new List<BreadcrumbItem>
                {
                    new() { Label = "Home", Url = "/" },
                    new() { Label = "Servers", Url = "/Guilds" },
                    new() { Label = guild.Name, Url = $"/Guilds/Details/{guild.Id}" },
                    new() { Label = "Audio", Url = $"/Guilds/Soundboard/{guild.Id}" },
                    new() { Label = "VOX", IsCurrent = true }
                }
            };

            Header = new GuildHeaderViewModel
            {
                GuildId = guild.Id,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                PageTitle = "VOX",
                PageDescription = $"Configure VOX announcements for {guild.Name}"
            };

            Navigation = new GuildNavBarViewModel
            {
                GuildId = guild.Id,
                ActiveTab = "audio",
                Tabs = GuildNavigationConfig.GetTabs().ToList()
            };

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load VOX page for guild {GuildId}", GuildId);
            ErrorMessage = "Failed to load VOX settings. Please try again.";
            return Page();
        }
    }

    /// <summary>
    /// Handles POST requests to rescan the clip library.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page.</returns>
    public async Task<IActionResult> OnPostRescanAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User requested VOX clip library rescan for guild {GuildId}", GuildId);

        try
        {
            await _voxClipLibrary.InitializeAsync(cancellationToken);
            SuccessMessage = "Clip library rescanned successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rescan VOX clip library for guild {GuildId}", GuildId);
            ErrorMessage = "Failed to rescan clip library. Please try again.";
        }

        return RedirectToPage("Index", new { guildId = GuildId, groupFilter = GroupFilter, searchQuery = SearchQuery, pageNumber = PageNumber });
    }
}
