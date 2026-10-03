using DiscordBot.Core.Extensions;
using System.Globalization;
using System.Text;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.RatWatch;

/// <summary>
/// Page model for the Rat Watch Incidents browser.
/// Displays a filterable, sortable, paginated list of all Rat Watch incidents for a guild.
/// </summary>
[Authorize(Policy = "RequireModerator")]
[Authorize(Policy = "GuildAccess")]
public class IncidentsModel : GuildPageModelBase
{
    /// <summary>
    /// The most rows one CSV export holds. An export reads the filtered incidents a page at a time
    /// and stops here, so a very large guild cannot fill the server's memory; the file name says
    /// when it was cut short.
    /// </summary>
    internal const int ExportRowCap = 10_000;

    private const int ExportPageSize = 500;

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

    /// <summary>
    /// The page of results. Bound as <c>pageNumber</c>: Razor Pages reserves <c>page</c> as the
    /// route key for the page name, so a link that carried <c>page=2</c> was overwritten on the way out.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

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
            guildId, PageNumber);

        // Get guild info from service
        var guild = await _guildService.GetGuildByIdAsync(ulongGuildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{ulongGuildId}" },
                new() { Label = "Rat Watch", Url = $"/Guilds/RatWatch/{ulongGuildId}" },
                new() { Label = "Incidents", IsCurrent = true }
            }
        };

        Header = BuildHeader(guild.Id, guild.Name, guild.IconUrl,
            "Rat Watch Incidents", $"Browse and filter all Rat Watch incidents for {guild.Name}");

        Navigation = BuildNavigation(guild.Id, "ratwatch");

        // Validate and normalize pagination parameters
        var normalizedPage = Math.Max(1, PageNumber);
        var normalizedPageSize = Math.Clamp(PageSize, 10, 100);
        var (filter, effectiveStartDate, effectiveEndDate) = BuildFilter(normalizedPage, normalizedPageSize);

        // The filter state the form shows, with the dates as typed (not the end-of-day the query uses)
        var filterState = new RatWatchIncidentFilterState
        {
            Statuses = filter.Statuses?.ToList() ?? new List<RatWatchStatus>(),
            StartDate = effectiveStartDate,
            EndDate = effectiveEndDate,
            DatesAreDefault = !StartDate.HasValue && !EndDate.HasValue,
            AccusedUser = filter.AccusedUser,
            InitiatorUser = filter.InitiatorUser,
            MinVoteCount = filter.MinVoteCount,
            Keyword = filter.Keyword,
            SortBy = filter.SortBy,
            SortDescending = filter.SortDescending
        };

        try
        {
            // Get guild settings for voting duration
            var settings = await _ratWatchService.GetGuildSettingsAsync(ulongGuildId, cancellationToken);

            var (incidents, totalCount) = await _ratWatchService.GetFilteredByGuildAsync(
                ulongGuildId,
                filter,
                cancellationToken);

            _logger.LogDebug(
                "Retrieved {Count} incidents for guild {GuildId} (page {Page} of {TotalPages}, {TotalCount} total)",
                incidents.Count(), guildId, normalizedPage,
                (int)Math.Ceiling((double)totalCount / normalizedPageSize), totalCount);

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
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to load Rat Watch incidents for guild {GuildId}", guildId);
            ErrorMessage = "The incidents could not be loaded. Try again in a moment.";
            ViewModel = new RatWatchIncidentsViewModel
            {
                GuildId = ulongGuildId,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                Filters = filterState,
                ActiveFilterCount = filterState.GetActiveFilterCount(),
                CurrentPage = normalizedPage,
                PageSize = normalizedPageSize
            };
        }

        return Page();
    }

    /// <summary>
    /// Builds the service filter from the bound query parameters. The page and the CSV export both
    /// use it, so an export holds exactly the rows the filters on screen select. With no dates given
    /// the range is the last 30 days, as the page has always shown.
    /// </summary>
    internal (RatWatchIncidentFilterDto Filter, DateTime? StartDate, DateTime? EndDate) BuildFilter(int page, int pageSize)
    {
        var effectiveStartDate = StartDate;
        var effectiveEndDate = EndDate;
        if (!StartDate.HasValue && !EndDate.HasValue)
        {
            effectiveStartDate = DateTime.Today.AddDays(-30);
            effectiveEndDate = DateTime.Today;
        }

        // The end date is inclusive: run to the last tick of that day
        DateTime? normalizedEndDate = effectiveEndDate.HasValue
            ? effectiveEndDate.Value.Date.AddDays(1).AddTicks(-1)
            : null;

        var filter = new RatWatchIncidentFilterDto
        {
            Statuses = Statuses?.Count > 0 ? Statuses : null,
            StartDate = effectiveStartDate,
            EndDate = normalizedEndDate,
            AccusedUser = AccusedUser,
            InitiatorUser = InitiatorUser,
            MinVoteCount = MinVoteCount,
            Keyword = Keyword,
            Page = page,
            PageSize = pageSize,
            SortBy = SortBy,
            SortDescending = SortDesc
        };

        return (filter, effectiveStartDate, effectiveEndDate);
    }

    /// <summary>
    /// Downloads every incident the current filters select (not just the page on screen) as CSV.
    /// Times are UTC and say so (UX decision D6); text that starts like a spreadsheet formula is
    /// neutralised, because names and messages come from guild members.
    /// </summary>
    public async Task<IActionResult> OnGetExportCsvAsync(long guildId, CancellationToken cancellationToken = default)
    {
        var ulongGuildId = (ulong)guildId;
        var (filter, _, _) = BuildFilter(page: 1, pageSize: ExportPageSize);

        var csv = new StringBuilder();
        csv.Append('﻿'); // lets Excel read the file as UTF-8
        csv.AppendLine("Scheduled (UTC),Accused,Initiator,Status,Votes For,Votes Against,Custom Message");

        var written = 0;
        var total = 0;
        try
        {
            while (true)
            {
                var (items, totalCount) = await _ratWatchService.GetFilteredByGuildAsync(ulongGuildId, filter, cancellationToken);
                total = totalCount;
                var rows = items.ToList();

                foreach (var incident in rows)
                {
                    if (written >= ExportRowCap)
                    {
                        break;
                    }

                    csv.AppendLine(FormatCsvRow(incident));
                    written++;
                }

                if (written >= ExportRowCap || written >= totalCount || rows.Count < filter.PageSize)
                {
                    break;
                }

                filter.Page++;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to export Rat Watch incidents for guild {GuildId}", guildId);
            TempData.SetErrorToast("The incidents could not be exported. Try again in a moment.");
            return RedirectToPage("Incidents", new { guildId });
        }

        _logger.LogInformation("Exported {Count} of {Total} Rat Watch incidents for guild {GuildId}", written, total, guildId);

        var truncated = total > ExportRowCap;
        var fileName = $"ratwatch-incidents-{guildId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}{(truncated ? $"-first-{ExportRowCap}" : string.Empty)}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv; charset=utf-8", fileName);
    }

    private static string FormatCsvRow(RatWatchDto incident)
    {
        var scheduled = DateTime.SpecifyKind(incident.ScheduledAt, DateTimeKind.Utc)
            .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return string.Join(',',
            scheduled,
            CsvCell(incident.AccusedUsername),
            CsvCell(incident.InitiatorUsername),
            CsvCell(incident.Status.DisplayName()),
            incident.GuiltyVotes.ToString(CultureInfo.InvariantCulture),
            incident.NotGuiltyVotes.ToString(CultureInfo.InvariantCulture),
            CsvCell(incident.CustomMessage));
    }

    /// <summary>One quoted CSV cell, with a leading formula character neutralised.</summary>
    internal static string CsvCell(string? value) =>
        "\"" + CsvField.NeutralizeFormula(value).Replace("\"", "\"\"") + "\"";

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
        [FromQuery] Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching incident detail for {IncidentId} in guild {GuildId}", incidentId, guildId);

        try
        {
            var watch = await _ratWatchService.GetByIdAsync(incidentId, cancellationToken);
            if (watch == null)
            {
                _logger.LogWarning("Incident {IncidentId} not found", incidentId);
                return new JsonResult(new { error = "Incident not found" }) { StatusCode = 404 };
            }

            // Verify watch belongs to this guild (cast to ulong for comparison)
            if (watch.GuildId != (ulong)guildId)
            {
                _logger.LogWarning(
                    "Incident {IncidentId} belongs to guild {ActualGuildId}, not requested guild {RequestedGuildId}",
                    incidentId, watch.GuildId, guildId);
                return new JsonResult(new { error = "Access denied" }) { StatusCode = 403 };
            }

            // Return incident details as JSON for modal display
            return new JsonResult(new
            {
                id = watch.Id,
                status = watch.Status.ToString(),
                statusText = watch.Status.DisplayName(),
                accusedUserId = watch.AccusedUserId.ToString(),
                accusedUsername = watch.AccusedUsername,
                initiatorUserId = watch.InitiatorUserId.ToString(),
                initiatorUsername = watch.InitiatorUsername,
                customMessage = watch.CustomMessage,
                scheduledAt = DisplayFormat.Iso(watch.ScheduledAt),
                createdAt = DisplayFormat.Iso(watch.CreatedAt),
                votingStartedAt = watch.VotingStartedAt.HasValue ? DisplayFormat.Iso(watch.VotingStartedAt.Value) : null,
                guiltyVotes = watch.GuiltyVotes,
                notGuiltyVotes = watch.NotGuiltyVotes,
                totalVotes = watch.GuiltyVotes + watch.NotGuiltyVotes,
                channelId = watch.ChannelId.ToString(),
                originalMessageId = watch.OriginalMessageId.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching incident detail for {IncidentId}", incidentId);
            return new JsonResult(new { error = "Internal server error" }) { StatusCode = 500 };
        }
    }
}
