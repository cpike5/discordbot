using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// API controller for loading Commands page tab content via AJAX.
/// Returns partial view HTML for each tab panel.
/// </summary>
[ApiController]
[Route("api/commands")]
[Authorize(Policy = "RequireViewer")]
public class CommandsApiController : Controller
{
    private const int MaxPageSize = 100;

    private readonly ICommandMetadataService _commandMetadataService;
    private readonly ICommandLogService _commandLogService;
    private readonly ICommandAnalyticsService _commandAnalyticsService;
    private readonly IGuildService _guildService;
    private readonly ILogger<CommandsApiController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandsApiController"/> class.
    /// </summary>
    public CommandsApiController(
        ICommandMetadataService commandMetadataService,
        ICommandLogService commandLogService,
        ICommandAnalyticsService commandAnalyticsService,
        IGuildService guildService,
        ILogger<CommandsApiController> logger)
    {
        _commandMetadataService = commandMetadataService;
        _commandLogService = commandLogService;
        _commandAnalyticsService = commandAnalyticsService;
        _guildService = guildService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the Command List tab content showing all registered command modules.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Partial view HTML for the command list tab.</returns>
    [HttpGet("list")]
    [Produces("text/html")]
    public async Task<IActionResult> GetCommandListTab(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Loading Command List tab content");

        try
        {
            var modules = await _commandMetadataService.GetAllModulesAsync(cancellationToken);
            var viewModel = CommandsListViewModel.FromDtos(modules);

            _logger.LogDebug(
                "Loaded {ModuleCount} modules with {CommandCount} total commands",
                viewModel.ModuleCount,
                viewModel.TotalCommandCount);

            return PartialView("~/Pages/Commands/Tabs/_CommandListTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Command List tab content");
            return Failure(500, "The command list could not be loaded. Try again.");
        }
    }

    /// <summary>
    /// Gets the Execution Logs tab content with filtering and pagination.
    /// </summary>
    /// <param name="startDate">Start date for date range filter.</param>
    /// <param name="endDate">End date for date range filter.</param>
    /// <param name="guildId">Guild ID filter.</param>
    /// <param name="searchTerm">Search term for multi-field search.</param>
    /// <param name="commandName">Command name filter.</param>
    /// <param name="statusFilter">Status filter (true=success, false=failure, null=all).</param>
    /// <param name="pageNumber">Current page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Partial view HTML for the execution logs tab.</returns>
    [HttpGet("logs")]
    [Produces("text/html")]
    public async Task<IActionResult> GetExecutionLogsTab(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] ulong? guildId,
        [FromQuery] string? searchTerm,
        [FromQuery] string? commandName,
        [FromQuery] bool? statusFilter,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Loading Execution Logs tab. Search={Search}, Guild={Guild}, Command={Command}, Status={Status}, Page={Page}",
            searchTerm, guildId, commandName, statusFilter, pageNumber);

        try
        {
            // Validate pagination parameters
            if (pageSize > MaxPageSize)
            {
                _logger.LogWarning("PageSize {PageSize} exceeds maximum of {MaxPageSize}, clamping", pageSize, MaxPageSize);
                pageSize = MaxPageSize;
            }

            if (pageNumber < 1)
            {
                _logger.LogWarning("PageNumber {PageNumber} is less than 1, defaulting to 1", pageNumber);
                pageNumber = 1;
            }

            var rangeError = ValidateDateRange(startDate, endDate);
            if (rangeError != null)
            {
                _logger.LogWarning("Invalid date range for command logs. Start={Start}, End={End}", startDate, endDate);
                return Failure(400, rangeError);
            }

            // Build query
            var query = new CommandLogQueryDto
            {
                SearchTerm = searchTerm,
                GuildId = guildId,
                CommandName = commandName,
                StartDate = startDate,
                EndDate = endDate,
                SuccessOnly = statusFilter,
                Page = pageNumber,
                PageSize = pageSize
            };

            // Fetch data
            var paginatedLogs = await _commandLogService.GetLogsAsync(query, cancellationToken);

            // A page past the end (a stale link, or rows deleted since) shows the last page instead of nothing
            if (paginatedLogs.Items.Count == 0 && paginatedLogs.TotalCount > 0 && pageNumber > 1)
            {
                query.Page = (int)Math.Ceiling(paginatedLogs.TotalCount / (double)pageSize);
                paginatedLogs = await _commandLogService.GetLogsAsync(query, cancellationToken);
            }

            var guilds = await _guildService.GetAllGuildsAsync(cancellationToken);

            // Build view model
            var filters = new CommandLogFilterOptions
            {
                SearchTerm = searchTerm,
                GuildId = guildId,
                CommandName = commandName,
                StartDate = startDate,
                EndDate = endDate,
                SuccessOnly = statusFilter
            };

            var viewModel = CommandLogListViewModel.FromPaginatedDto(paginatedLogs, filters);

            // Store guilds in ViewData for the partial view
            ViewData["AvailableGuilds"] = guilds;

            // Pagination links are real deep links to the page (the page script fetches instead)
            ViewData["PaginationBaseUrl"] = Url.Page("/Commands/Index", new
            {
                tab = "execution-logs",
                StartDate = FormatDate(startDate),
                EndDate = FormatDate(endDate),
                GuildId = guildId,
                CommandName = string.IsNullOrWhiteSpace(commandName) ? null : commandName,
                SearchTerm = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm,
                StatusFilter = statusFilter
            });

            _logger.LogDebug(
                "Loaded {LogCount} logs (page {Page} of {TotalPages})",
                viewModel.Logs.Count, viewModel.CurrentPage, viewModel.TotalPages);

            return PartialView("~/Pages/Commands/Tabs/_ExecutionLogsTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Execution Logs tab content");
            return Failure(500, "Command logs could not be loaded. Try again.");
        }
    }

    /// <summary>
    /// Gets the Analytics tab content with date range and guild filtering.
    /// </summary>
    /// <param name="startDate">Start date for analytics period (defaults to 30 days ago).</param>
    /// <param name="endDate">End date for analytics period (defaults to today).</param>
    /// <param name="guildId">Guild ID filter (null = all guilds).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Partial view HTML for the analytics tab.</returns>
    [HttpGet("analytics")]
    [Produces("text/html")]
    public async Task<IActionResult> GetAnalyticsTab(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] ulong? guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading Analytics tab. Start={Start}, End={End}, Guild={Guild}",
            startDate, endDate, guildId);

        try
        {
            // Apply defaults
            var end = endDate ?? DateTime.UtcNow.Date;
            var start = startDate ?? end.AddDays(-30);

            var rangeError = ValidateDateRange(start, end);
            if (rangeError != null)
            {
                _logger.LogWarning("Invalid date range for analytics. Start={Start}, End={End}", start, end);
                return Failure(400, rangeError);
            }

            // Fetch analytics data
            var analyticsData = await _commandAnalyticsService.GetAnalyticsAsync(
                start, end, guildId, cancellationToken);

            var guilds = await _guildService.GetAllGuildsAsync(cancellationToken);

            // Build view model
            ViewData["HasActiveFilters"] = guildId.HasValue || startDate.HasValue || endDate.HasValue;

            var viewModel = new CommandAnalyticsViewModel
            {
                TotalCommands = analyticsData.TotalCommands,
                SuccessRate = analyticsData.SuccessRate,
                AvgResponseTimeMs = analyticsData.AvgResponseTimeMs,
                UniqueCommands = analyticsData.UniqueCommands,
                UsageOverTime = analyticsData.UsageOverTime,
                TopCommands = analyticsData.TopCommands,
                SuccessRateData = analyticsData.SuccessRateData,
                PerformanceData = analyticsData.PerformanceData,
                StartDate = start,
                EndDate = end,
                GuildId = guildId,
                AvailableGuilds = guilds
                    .Select(g => new GuildSelectOption(g.Id, g.Name))
                    .ToList()
            };

            _logger.LogDebug(
                "Loaded analytics data. Total={Total}, Success={Success}%, Avg={Avg}ms",
                viewModel.TotalCommands, viewModel.SuccessRate, viewModel.AvgResponseTimeMs);

            return PartialView("~/Pages/Commands/Tabs/_AnalyticsTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Analytics tab content");
            return Failure(500, "Analytics could not be loaded. Try again.");
        }
    }

    /// <summary>
    /// Gets the command log details content for the modal.
    /// </summary>
    /// <param name="id">The command log ID (GUID).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Partial view HTML for the command log details modal content.</returns>
    [HttpGet("log-details/{id:guid}")]
    [Produces("text/html")]
    public async Task<IActionResult> GetLogDetails(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Loading command log details for ID {LogId}", id);

        try
        {
            var log = await _commandLogService.GetByIdAsync(id, cancellationToken);
            if (log == null)
            {
                _logger.LogWarning("Command log not found: {LogId}", id);
                return Failure(404, "That command log no longer exists.");
            }

            var viewModel = ViewModels.Components.CommandLogDetailsModalViewModel.FromDto(log);

            _logger.LogDebug(
                "Loaded command log details: {Command}, Success={Success}",
                viewModel.CommandName, viewModel.IsSuccess);

            return PartialView("~/Pages/CommandLogs/_CommandLogDetailsContent.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load command log details for ID {LogId}", id);
            return Failure(500, "The command log could not be loaded. Try again.");
        }
    }

    #region Helpers

    /// <summary>The longest date range a logs or analytics request may cover.</summary>
    internal const int MaxRangeDays = 90;

    /// <summary>
    /// Checks a date range. Returns the message to show next to the date fields, or null when
    /// the range is fine (either end may be missing).
    /// </summary>
    internal static string? ValidateDateRange(DateTime? start, DateTime? end)
    {
        if (!start.HasValue || !end.HasValue)
        {
            return null;
        }

        if (start.Value.Date > end.Value.Date)
        {
            return "The start date must be on or before the end date.";
        }

        if ((end.Value.Date - start.Value.Date).TotalDays > MaxRangeDays)
        {
            return $"Choose a date range of {MaxRangeDays} days or less.";
        }

        return null;
    }

    private static string? FormatDate(DateTime? date) => date?.ToString("yyyy-MM-dd");

    /// <summary>
    /// A failed tab load as problem JSON, which the page script shows as plain text. The
    /// <c>detail</c> is copy written for the user, never exception text. (The action's
    /// <c>[Produces("text/html")]</c> would try to format an object as HTML, so the body is
    /// written out as a content result.)
    /// </summary>
    private static ContentResult Failure(int statusCode, string detail)
    {
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            title = statusCode >= 500 ? "Could not load" : "Request not valid",
            status = statusCode,
            detail
        });

        return new ContentResult
        {
            Content = body,
            ContentType = "application/problem+json",
            StatusCode = statusCode
        };
    }

    #endregion
}
