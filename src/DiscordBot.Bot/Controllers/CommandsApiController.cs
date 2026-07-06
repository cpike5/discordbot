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
    private readonly ICommandAnalyticsService _commandAnalyticsService;
    private readonly IGuildService _guildService;
    private readonly ILogger<CommandsApiController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandsApiController"/> class.
    /// </summary>
    public CommandsApiController(
        ICommandAnalyticsService commandAnalyticsService,
        IGuildService guildService,
        ILogger<CommandsApiController> logger)
    {
        _commandAnalyticsService = commandAnalyticsService;
        _guildService = guildService;
        _logger = logger;
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
