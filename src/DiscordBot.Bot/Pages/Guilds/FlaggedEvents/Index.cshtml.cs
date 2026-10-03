using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace DiscordBot.Bot.Pages.Guilds.FlaggedEvents;

/// <summary>
/// Page model for the Flagged Events list page.
/// Displays auto-detected moderation events with filtering and bulk review.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : GuildPageModelBase
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
    /// Current page number. Bound as <c>pageNumber</c>: Razor Pages reserves <c>page</c> for the page name.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Page size.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "pageSize")]
    public int CurrentPageSize { get; set; } = 20;

    /// <summary>
    /// Total number of events matching filters.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Total pages.
    /// </summary>
    public int TotalPages => CurrentPageSize > 0 ? (int)Math.Ceiling((double)TotalCount / CurrentPageSize) : 0;

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
    /// Whether any filter is set. With none, the list shows every event the server has flagged.
    /// </summary>
    public bool HasActiveFilters =>
        FilterRuleType.HasValue || FilterSeverity.HasValue || FilterStatus.HasValue ||
        FilterDateFrom.HasValue || FilterDateTo.HasValue;

    /// <summary>
    /// Handles GET requests to display the flagged events list.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        // Validate pagination parameters
        if (CurrentPage < 1) CurrentPage = 1;
        if (CurrentPageSize < 1 || CurrentPageSize > 100) CurrentPageSize = 20;

        GuildId = guildId;

        _logger.LogInformation("User accessing Flagged Events list for guild {GuildId}, page {Page}, filters: RuleType={RuleType}, Severity={Severity}, Status={Status}, DateFrom={DateFrom}, DateTo={DateTo}",
            guildId, CurrentPage, FilterRuleType, FilterSeverity, FilterStatus, FilterDateFrom, FilterDateTo);

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
            Page = CurrentPage,
            PageSize = CurrentPageSize
        };

        // Get filtered events
        var (events, totalCount) = await _flaggedEventService.GetFilteredEventsAsync(
            guildId,
            query,
            cancellationToken);

        Events = events;
        TotalCount = totalCount;

        // Reviewing the last rows of the last page leaves that page empty: go to the new last page
        if (!Events.Any() && TotalCount > 0 && CurrentPage > TotalPages)
        {
            return RedirectToPage("/Guilds/FlaggedEvents/Index", ListRouteValues(TotalPages));
        }

        _logger.LogDebug("Retrieved {Count} flagged events for guild {GuildId} (page {Page} of {TotalPages})",
            Events.Count(), guildId, CurrentPage, TotalPages);

        // Populate guild layout ViewModels
        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{guildId}" },
                new() { Label = "Moderation", Url = $"/Guilds/ModerationSettings/{guildId}" },
                new() { Label = "Flagged Events", IsCurrent = true }
            }
        };

        Header = BuildHeader(guild.Id, guild.Name, guild.IconUrl,
            "Flagged Events", $"Auto-detected moderation events for {guild.Name}");

        Navigation = BuildNavigation(guild.Id, "moderation");

        return Page();
    }

    /// <summary>
    /// The list's current filters as route values, so links and redirects keep the reader's place.
    /// </summary>
    public RouteValueDictionary ListRouteValues(int? pageNumber = null)
    {
        var values = new RouteValueDictionary { ["guildId"] = GuildId, ["pageNumber"] = pageNumber ?? CurrentPage };
        if (FilterRuleType.HasValue) values["FilterRuleType"] = FilterRuleType.Value;
        if (FilterSeverity.HasValue) values["FilterSeverity"] = FilterSeverity.Value;
        if (FilterStatus.HasValue) values["FilterStatus"] = FilterStatus.Value;
        if (FilterDateFrom.HasValue) values["FilterDateFrom"] = FilterDateFrom.Value.ToString("yyyy-MM-dd");
        if (FilterDateTo.HasValue) values["FilterDateTo"] = FilterDateTo.Value.ToString("yyyy-MM-dd");
        if (CurrentPageSize != 20) values["pageSize"] = CurrentPageSize;
        return values;
    }

    /// <summary>
    /// Dismisses one event from its row.
    /// </summary>
    public Task<IActionResult> OnPostDismissAsync(ulong guildId, Guid id, CancellationToken cancellationToken)
        => ApplyAsync(guildId, FlaggedEventReviewAction.Dismiss, new[] { id }, cancellationToken);

    /// <summary>
    /// Acknowledges one event from its row.
    /// </summary>
    public Task<IActionResult> OnPostAcknowledgeAsync(ulong guildId, Guid id, CancellationToken cancellationToken)
        => ApplyAsync(guildId, FlaggedEventReviewAction.Acknowledge, new[] { id }, cancellationToken);

    /// <summary>
    /// Dismisses or acknowledges the selected events, then reports how many changed.
    /// </summary>
    public Task<IActionResult> OnPostBulkAsync(
        ulong guildId, string? bulkAction, List<Guid>? ids, CancellationToken cancellationToken)
    {
        FlaggedEventReviewAction? action = null;
        if (string.Equals(bulkAction, "dismiss", StringComparison.OrdinalIgnoreCase))
        {
            action = FlaggedEventReviewAction.Dismiss;
        }
        else if (string.Equals(bulkAction, "acknowledge", StringComparison.OrdinalIgnoreCase))
        {
            action = FlaggedEventReviewAction.Acknowledge;
        }

        if (action == null)
        {
            GuildId = guildId;
            TempData.SetErrorToast("Choose Dismiss or Acknowledge for the selected events.");
            return Task.FromResult<IActionResult>(RedirectToPage("/Guilds/FlaggedEvents/Index", ListRouteValues()));
        }

        var distinct = (ids ?? new List<Guid>()).Distinct().Take(100).ToList();
        return ApplyAsync(guildId, action.Value, distinct, cancellationToken);
    }

    private async Task<IActionResult> ApplyAsync(
        ulong guildId, FlaggedEventReviewAction action, IReadOnlyList<Guid> ids, CancellationToken cancellationToken)
    {
        GuildId = guildId;

        // Never record 0 as the reviewer: an admin without a linked Discord account has no ID
        if (!User.TryGetDiscordUserId(out var reviewerId))
        {
            TempData.SetErrorToast(FlaggedEventReviewRules.LinkDiscordMessage);
            return RedirectToPage("/Guilds/FlaggedEvents/Index", ListRouteValues());
        }

        if (ids.Count == 0)
        {
            TempData.SetErrorToast("Select at least one event first.");
            return RedirectToPage("/Guilds/FlaggedEvents/Index", ListRouteValues());
        }

        var outcome = new FlaggedEventBatchOutcome();

        foreach (var id in ids)
        {
            var existing = await _flaggedEventService.GetEventAsync(id, cancellationToken);
            if (existing == null || existing.GuildId != guildId)
            {
                outcome.NotFound++;
                continue;
            }

            if (!FlaggedEventReviewRules.CanApply(action, existing.Status))
            {
                outcome.Skipped++;
                continue;
            }

            try
            {
                var updated = action == FlaggedEventReviewAction.Dismiss
                    ? await _flaggedEventService.DismissEventAsync(id, reviewerId, cancellationToken)
                    : await _flaggedEventService.AcknowledgeEventAsync(id, reviewerId, cancellationToken);

                if (updated == null) outcome.NotFound++; else outcome.Done++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to {Action} flagged event {EventId} in guild {GuildId}", action, id, guildId);
                outcome.Failed++;
            }
        }

        var (kind, message) = outcome.Describe(action, ids.Count);
        switch (kind)
        {
            case FlaggedEventBatchOutcome.ToastKind.Success: TempData.SetSuccessToast(message); break;
            case FlaggedEventBatchOutcome.ToastKind.Warning: TempData.SetWarningToast(message); break;
            default: TempData.SetErrorToast(message); break;
        }

        return RedirectToPage("/Guilds/FlaggedEvents/Index", ListRouteValues());
    }
}
