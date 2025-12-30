using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.RatWatch;

/// <summary>
/// Page model for the Rat Watch Incidents browser.
/// Displays a filterable, sortable, paginated list of all Rat Watch incidents for a guild.
/// </summary>
[Authorize(Policy = "RequireModeratorOrAbove")]
public class IncidentsModel : PageModel
{
    private readonly IRatWatchService _ratWatchService;
    private readonly IGuildService _guildService;
    private readonly ILogger<IncidentsModel> _logger;

    public IncidentsModel(
        IRatWatchService ratWatchService,
        IGuildService guildService,
        ILogger<IncidentsModel> logger)
    {
        _ratWatchService = ratWatchService;
        _guildService = guildService;
        _logger = logger;
    }

    /// <summary>
    /// View model for display properties.
    /// </summary>
    public RatWatchIncidentsViewModel ViewModel { get; set; } = new();

    // Filter parameters bound from query string
    [BindProperty(SupportsGet = true)]
    public List<RatWatchStatus>? Statuses { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AccusedUser { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? InitiatorUser { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? MinVoteCount { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public new int Page { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    [BindProperty(SupportsGet = true)]
    public string SortBy { get; set; } = "ScheduledAt";

    [BindProperty(SupportsGet = true)]
    public bool SortDesc { get; set; } = true;

    /// <summary>
    /// Handles GET requests to display the incidents browser page.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(long guildId, CancellationToken cancellationToken = default)
    {
        var ulongGuildId = (ulong)guildId;

        _logger.LogInformation(
            "User accessing Rat Watch Incidents browser for guild {GuildId}, page {Page}",
            guildId, Page);

        // Get guild info from service
        var guild = await _guildService.GetGuildByIdAsync(ulongGuildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        // Get guild settings for voting duration
        var settings = await _ratWatchService.GetGuildSettingsAsync(ulongGuildId, cancellationToken);

        // Validate and normalize pagination parameters
        var normalizedPage = Math.Max(1, Page);
        var normalizedPageSize = Math.Clamp(PageSize, 10, 100);

        // Build filter DTO from bound properties
        var filter = new RatWatchIncidentFilterDto
        {
            Statuses = Statuses?.Count > 0 ? Statuses : null,
            StartDate = StartDate,
            EndDate = EndDate,
            AccusedUser = AccusedUser,
            InitiatorUser = InitiatorUser,
            MinVoteCount = MinVoteCount,
            Keyword = Keyword,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            SortBy = SortBy,
            SortDescending = SortDesc
        };

        // Get filtered incidents
        var (incidents, totalCount) = await _ratWatchService.GetFilteredByGuildAsync(
            ulongGuildId,
            filter,
            cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} incidents for guild {GuildId} (page {Page} of {TotalPages}, {TotalCount} total)",
            incidents.Count(), guildId, normalizedPage,
            (int)Math.Ceiling((double)totalCount / normalizedPageSize), totalCount);

        // Build view model
        var filterState = RatWatchIncidentFilterState.FromDto(filter);
        ViewModel = RatWatchIncidentsViewModel.Create(
            ulongGuildId,
            guild.Name,
            guild.IconUrl,
            incidents,
            totalCount,
            filterState,
            normalizedPage,
            normalizedPageSize,
            settings?.VotingDurationMinutes ?? 5);

        return Page();
    }

    /// <summary>
    /// AJAX handler to get incident details for the modal.
    /// Returns JSON with full incident details.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="incidentId">The incident ID to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JSON result with incident details, or NotFound if incident doesn't exist.</returns>
    public async Task<IActionResult> OnGetIncidentDetailAsync(
        long guildId,
        Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching incident detail for {IncidentId} in guild {GuildId}", incidentId, guildId);

        var watch = await _ratWatchService.GetByIdAsync(incidentId, cancellationToken);
        if (watch == null)
        {
            _logger.LogWarning("Incident {IncidentId} not found", incidentId);
            return NotFound();
        }

        // Verify watch belongs to this guild
        if (watch.GuildId != (ulong)guildId)
        {
            _logger.LogWarning(
                "Incident {IncidentId} belongs to guild {ActualGuildId}, not requested guild {RequestedGuildId}",
                incidentId, watch.GuildId, guildId);
            return Forbid();
        }

        // Return incident details as JSON for modal display
        return new JsonResult(new
        {
            id = watch.Id,
            status = watch.Status.ToString(),
            statusText = GetStatusText(watch.Status),
            accusedUserId = watch.AccusedUserId.ToString(),
            accusedUsername = watch.AccusedUsername,
            initiatorUserId = watch.InitiatorUserId.ToString(),
            initiatorUsername = watch.InitiatorUsername,
            customMessage = watch.CustomMessage,
            scheduledAt = watch.ScheduledAt.ToString("o"),
            createdAt = watch.CreatedAt.ToString("o"),
            votingStartedAt = watch.VotingStartedAt?.ToString("o"),
            guiltyVotes = watch.GuiltyVotes,
            notGuiltyVotes = watch.NotGuiltyVotes,
            totalVotes = watch.GuiltyVotes + watch.NotGuiltyVotes,
            channelId = watch.ChannelId.ToString(),
            originalMessageId = watch.OriginalMessageId.ToString()
        });
    }

    /// <summary>
    /// Gets the display text for a status enum value.
    /// </summary>
    /// <param name="status">The status to convert to text.</param>
    /// <returns>The human-readable status text.</returns>
    private static string GetStatusText(RatWatchStatus status) => status switch
    {
        RatWatchStatus.Pending => "Pending",
        RatWatchStatus.ClearedEarly => "Cleared Early",
        RatWatchStatus.Voting => "Voting",
        RatWatchStatus.Guilty => "Guilty",
        RatWatchStatus.NotGuilty => "Not Guilty",
        RatWatchStatus.Expired => "Expired",
        RatWatchStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };
}
