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
[Authorize(Policy = "RequireModerator")]
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
            return StatusCode(500, CreateErrorHtml("Failed to load command list data"));
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

            // Validate date range (max 90 days)
            if (startDate.HasValue && endDate.HasValue)
            {
                var dateRange = (endDate.Value - startDate.Value).TotalDays;
                if (dateRange > 90)
                {
                    _logger.LogWarning(
                        "Date range exceeds 90 days. Start={Start}, End={End}",
                        startDate, endDate);
                    return BadRequest(CreateErrorHtml("Date range cannot exceed 90 days"));
                }
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

            _logger.LogDebug(
                "Loaded {LogCount} logs (page {Page} of {TotalPages})",
                viewModel.Logs.Count, viewModel.CurrentPage, viewModel.TotalPages);

            return PartialView("~/Pages/Commands/Tabs/_ExecutionLogsTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Execution Logs tab content");
            return StatusCode(500, CreateErrorHtml("Failed to load command logs"));
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

            // Validate date range (max 90 days)
            if ((end - start).TotalDays > 90)
            {
                _logger.LogWarning(
                    "Date range exceeds 90 days. Start={Start}, End={End}",
                    start, end);
                return BadRequest(CreateErrorHtml("Date range cannot exceed 90 days"));
            }

            // Fetch analytics data
            var analyticsData = await _commandAnalyticsService.GetAnalyticsAsync(
                start, end, guildId, cancellationToken);

            var guilds = await _guildService.GetAllGuildsAsync(cancellationToken);

            // Build view model
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
            return StatusCode(500, CreateErrorHtml("Failed to load analytics data"));
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
                return NotFound(CreateErrorHtml("Command log not found"));
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
            return StatusCode(500, CreateErrorHtml("Failed to load command log details"));
        }
    }

    #region Helpers

    /// <summary>
    /// Creates an HTML error state for display in tabs.
    /// </summary>
    /// <param name="message">The error message to display.</param>
    /// <returns>A ContentResult containing formatted error HTML.</returns>
    private static ContentResult CreateErrorHtml(string message)
    {
        var html = $@"
<div class=""tab-error-state"">
    <div class=""tab-error-content"">
        <svg class=""tab-error-icon"" fill=""none"" viewBox=""0 0 24 24"" stroke=""currentColor"">
            <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"" />
        </svg>
        <h3 class=""tab-error-title"">Error Loading Content</h3>
        <p class=""tab-error-message"">{System.Web.HttpUtility.HtmlEncode(message)}</p>
        <button class=""btn btn-secondary tab-retry-btn"" onclick=""window.CommandTabs?.retryCurrentTab()"">
            <svg class=""btn-svg-icon"" fill=""none"" viewBox=""0 0 24 24"" stroke=""currentColor"">
                <path stroke-linecap=""round"" stroke-linejoin=""round"" stroke-width=""2"" d=""M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"" />
            </svg>
            Retry
        </button>
    </div>
</div>";

        return new ContentResult
        {
            Content = html,
            ContentType = "text/html",
            StatusCode = 500
        };
    }

    #endregion
}
