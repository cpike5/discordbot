using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.FlaggedEvents;

/// <summary>
/// Page model for the Flagged Events list page.
/// Displays auto-detected moderation events with filtering and bulk actions.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class IndexModel : PageModel
{
    private readonly IFlaggedEventService _flaggedEventService;
    private readonly IGuildService _guildService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IFlaggedEventService flaggedEventService,
        IGuildService guildService,
        ILogger<IndexModel> logger)
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
    /// The list of flagged events.
    /// </summary>
    public IEnumerable<FlaggedEventDto> Events { get; set; } = Array.Empty<FlaggedEventDto>();

    /// <summary>
    /// Current page number.
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    /// Page size.
    /// </summary>
    public int CurrentPageSize { get; set; }

    /// <summary>
    /// Total number of events matching filters.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Total pages.
    /// </summary>
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / CurrentPageSize);

    /// <summary>
    /// Current filters applied.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public RuleType? FilterRuleType { get; set; }

    [BindProperty(SupportsGet = true)]
    public Severity? FilterSeverity { get; set; }

    [BindProperty(SupportsGet = true)]
    public FlaggedEventStatus? FilterStatus { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FilterDateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FilterDateTo { get; set; }

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
    /// Handles GET requests to display the flagged events list.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="page">The page number from query parameter (default: 1).</param>
    /// <param name="pageSize">The page size from query parameter (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User accessing Flagged Events list for guild {GuildId}, page {Page}, filters: RuleType={RuleType}, Severity={Severity}, Status={Status}",
            guildId, page, FilterRuleType, FilterSeverity, FilterStatus);

        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        GuildId = guildId;
        CurrentPage = page;
        CurrentPageSize = pageSize;

        // Get guild info from service
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        GuildName = guild.Name;

        // Build query with filters
        var query = new FlaggedEventQueryDto
        {
            RuleType = FilterRuleType,
            Severity = FilterSeverity,
            Status = FilterStatus,
            DateFrom = FilterDateFrom,
            DateTo = FilterDateTo,
            Page = page,
            PageSize = pageSize
        };

        // Get filtered events
        var (events, totalCount) = await _flaggedEventService.GetFilteredEventsAsync(
            guildId,
            query,
            cancellationToken);

        Events = events;
        TotalCount = totalCount;

        _logger.LogDebug("Retrieved {Count} flagged events for guild {GuildId} (page {Page} of {TotalPages})",
            Events.Count(), guildId, CurrentPage, TotalPages);

        return Page();
    }
}
