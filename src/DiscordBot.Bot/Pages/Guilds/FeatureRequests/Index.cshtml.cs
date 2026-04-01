using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.FeatureRequests;

/// <summary>
/// Page model for the Feature Requests list page.
/// Displays feature request submissions for a guild with status filtering and pagination.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : GuildPageModelBase
{
    private readonly IFeatureRequestService _service;
    private readonly IGuildService _guildService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IFeatureRequestService service,
        IGuildService guildService,
        ILogger<IndexModel> logger)
    {
        _service = service;
        _guildService = guildService;
        _logger = logger;
    }

    public ulong GuildId { get; set; }
    public string GuildName { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public FeatureRequestStatus? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public new int Page { get; set; } = 1;

    public IEnumerable<FeatureRequest> Items { get; private set; } = [];
    public int Total { get; private set; }
    public int PageSize { get; } = 20;
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);

    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        if (Page < 1) Page = 1;

        GuildId = guildId;

        _logger.LogInformation(
            "User accessing Feature Requests list for guild {GuildId}, page {Page}, status filter {StatusFilter}",
            guildId, Page, StatusFilter);

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        GuildName = guild.Name;

        (Items, Total) = await _service.GetByGuildIdAsync(guildId, StatusFilter, Page, PageSize);

        _logger.LogDebug(
            "Retrieved {Count} feature requests for guild {GuildId} (page {Page} of {TotalPages})",
            Items.Count(), guildId, Page, TotalPages);

        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{guildId}" },
                new() { Label = "Feature Requests", IsCurrent = true }
            }
        };

        Header = BuildHeader(guild.Id, guild.Name, guild.IconUrl,
            "Feature Requests", $"Community feature requests for {guild.Name}");

        Navigation = BuildNavigation(guild.Id, "feature-requests");

        return Page();
    }
}
