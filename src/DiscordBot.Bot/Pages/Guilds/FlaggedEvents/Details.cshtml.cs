using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.FlaggedEvents;

/// <summary>
/// Page model for the Flagged Event details page.
/// Displays full event details, evidence, and action panel.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class DetailsModel : PageModel
{
    private readonly IFlaggedEventService _flaggedEventService;
    private readonly IGuildService _guildService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        IFlaggedEventService flaggedEventService,
        IGuildService guildService,
        ILogger<DetailsModel> logger)
    {
        _flaggedEventService = flaggedEventService;
        _guildService = guildService;
        _logger = logger;
    }

    /// <summary>
    /// The guild ID from route parameter.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// The guild name for display.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

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
    /// The flagged event details.
    /// </summary>
    public FlaggedEventDto Event { get; set; } = null!;

    /// <summary>
    /// User's other flagged events for history.
    /// </summary>
    public IEnumerable<FlaggedEventDto> UserHistory { get; set; } = Array.Empty<FlaggedEventDto>();

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
    /// Handles GET requests to display the flagged event details.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="id">The event ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User accessing Flagged Event details for event {EventId} in guild {GuildId}",
            id, guildId);

        GuildId = guildId;

        // Get guild info from service
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        GuildName = guild.Name;

        // Get event details
        var eventDto = await _flaggedEventService.GetEventAsync(id, cancellationToken);
        if (eventDto == null || eventDto.GuildId != guildId)
        {
            _logger.LogWarning("Flagged event {EventId} not found for guild {GuildId}", id, guildId);
            return NotFound();
        }

        Event = eventDto;

        // Get user's other flagged events for history (limit to last 10, excluding current event)
        var userEvents = await _flaggedEventService.GetUserEventsAsync(guildId, Event.UserId, cancellationToken);
        UserHistory = userEvents
            .Where(e => e.Id != id)
            .OrderByDescending(e => e.CreatedAt)
            .Take(10);

        _logger.LogDebug("Retrieved flagged event {EventId} details with {HistoryCount} user history items",
            id, UserHistory.Count());

        // Populate guild layout ViewModels
        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{guildId}" },
                new() { Label = "Moderation", Url = $"/Guilds/{guildId}/FlaggedEvents" },
                new() { Label = "Flagged Events", Url = $"/Guilds/{guildId}/FlaggedEvents" },
                new() { Label = "Details", IsCurrent = true }
            }
        };

        Header = new GuildHeaderViewModel
        {
            GuildId = guild.Id,
            GuildName = guild.Name,
            GuildIconUrl = guild.IconUrl,
            PageTitle = "Flagged Event Details",
            PageDescription = $"Event details for {guild.Name}"
        };

        Navigation = new GuildNavBarViewModel
        {
            GuildId = guild.Id,
            ActiveTab = "moderation",
            Tabs = GuildNavigationConfig.GetTabs().ToList()
        };

        return Page();
    }
}
