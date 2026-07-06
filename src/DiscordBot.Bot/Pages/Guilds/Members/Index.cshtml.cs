using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.Members;

/// <summary>
/// Host page for the guild member directory. Renders the guild layout chrome
/// (breadcrumb, header, navigation) and mounts the Blazor
/// <see cref="Blazor.Pages.MemberDirectoryIsland"/>, which owns filtering,
/// sorting, pagination, bulk export, and the member detail modal.
/// </summary>
[Authorize(Policy = "RequireModerator")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : PaginatedGuildPageModel
{
    private readonly IGuildService _guildService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IGuildService guildService,
        ILogger<IndexModel> logger)
    {
        _guildService = guildService;
        _logger = logger;

        // Override base class defaults for member directory
        SortBy = "JoinedAt";
        SortDescending = true;
        PageSize = 25;
    }

    /// <summary>
    /// The Discord guild snowflake ID from route.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Search term for filtering by username, display name, or user ID.
    /// Passed to the island as its initial state.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by role IDs (comma-separated in query string).
    /// Passed to the island as its initial state.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public List<ulong>? RoleFilter { get; set; }

    /// <summary>
    /// Filter by join date start (inclusive). Passed to the island as its initial state.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? JoinedAfter { get; set; }

    /// <summary>
    /// Filter by join date end (inclusive). Passed to the island as its initial state.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? JoinedBefore { get; set; }

    /// <summary>
    /// Filter by activity status. Passed to the island as its initial state.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? ActivityFilter { get; set; }

    /// <summary>
    /// The guild information.
    /// </summary>
    public GuildDto? Guild { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "User accessing member directory for guild {GuildId}. Search={Search}, Sort={Sort}",
            GuildId, SearchTerm, SortBy);

        // Get guild info
        Guild = await _guildService.GetGuildByIdAsync(GuildId, cancellationToken);
        if (Guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", GuildId);
            return NotFound();
        }

        // Populate guild layout ViewModels
        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = Guild.Name, Url = $"/Guilds/Details/{Guild.Id}" },
                new() { Label = "Members", IsCurrent = true }
            }
        };

        Header = new GuildHeaderViewModel
        {
            GuildId = Guild.Id,
            GuildName = Guild.Name,
            GuildIconUrl = Guild.IconUrl,
            PageTitle = "Members",
            PageDescription = $"Manage members for {Guild.Name}",
            Actions = new List<HeaderAction>
            {
                new()
                {
                    Label = "Export CSV",
                    Url = $"/api/guilds/{Guild.Id}/members/export",
                    Icon = "M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4",
                    Style = HeaderActionStyle.Secondary
                }
            }
        };

        Navigation = new GuildNavBarViewModel
        {
            GuildId = Guild.Id,
            ActiveTab = "members",
            Tabs = GuildNavigationConfig.GetTabs().ToList()
        };

        return Page();
    }
}
