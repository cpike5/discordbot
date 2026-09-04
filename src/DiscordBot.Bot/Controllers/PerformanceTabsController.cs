using DiscordBot.Bot.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// API controller for loading Performance Dashboard tab content via AJAX.
/// Returns partial view HTML for each tab panel. All data aggregation is delegated to
/// <see cref="IPerformanceDashboardAggregator"/>.
/// </summary>
[ApiController]
[Route("api/performance/tabs")]
[Authorize(Policy = "RequireViewer")]
public class PerformanceTabsController : Controller
{
    private readonly IPerformanceDashboardAggregator _aggregator;
    private readonly ILogger<PerformanceTabsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceTabsController"/> class.
    /// </summary>
    public PerformanceTabsController(
        IPerformanceDashboardAggregator aggregator,
        ILogger<PerformanceTabsController> logger)
    {
        _aggregator = aggregator;
        _logger = logger;
    }

    /// <summary>
    /// Gets the Overview tab content.
    /// </summary>
    /// <param name="hours">Time range in hours (24, 168, or 720).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Partial view HTML for the overview tab.</returns>
    [HttpGet("overview")]
    [Produces("text/html")]
    public async Task<IActionResult> GetOverviewTab([FromQuery] int hours = 24, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading Overview tab content for {Hours} hours", hours);

        try
        {
            var result = await _aggregator.BuildOverviewAsync(hours, cancellationToken);
            return PartialView("~/Pages/Admin/Performance/Tabs/_OverviewTab.cshtml", result.Overview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Overview tab content");
            return StatusCode(500, CreateErrorHtml("Failed to load overview data"));
        }
    }

    /// <summary>
    /// Gets the Health Metrics tab content.
    /// </summary>
    /// <param name="hours">Time range in hours (24, 168, or 720).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Partial view HTML for the health tab.</returns>
    [HttpGet("health")]
    [Produces("text/html")]
    public async Task<IActionResult> GetHealthTab([FromQuery] int hours = 24, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading Health tab content for {Hours} hours", hours);

        try
        {
            var viewModel = await _aggregator.BuildHealthMetricsAsync(cancellationToken);
            return PartialView("~/Pages/Admin/Performance/Tabs/_HealthTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Health tab content");
            return StatusCode(500, CreateErrorHtml("Failed to load health metrics"));
        }
    }

    /// <summary>
    /// Gets the Commands tab content.
    /// </summary>
    /// <param name="hours">Time range in hours (24, 168, or 720).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Partial view HTML for the commands tab.</returns>
    [HttpGet("commands")]
    [Produces("text/html")]
    public async Task<IActionResult> GetCommandsTab([FromQuery] int hours = 24, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading Commands tab content for {Hours} hours", hours);

        try
        {
            var viewModel = await _aggregator.BuildCommandPerformanceAsync(hours, cancellationToken);
            return PartialView("~/Pages/Admin/Performance/Tabs/_CommandsTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Commands tab content");
            return StatusCode(500, CreateErrorHtml("Failed to load command performance data"));
        }
    }

    /// <summary>
    /// Gets the API Metrics tab content.
    /// </summary>
    /// <param name="hours">Time range in hours (24, 168, or 720).</param>
    /// <returns>Partial view HTML for the API tab.</returns>
    [HttpGet("api")]
    [Produces("text/html")]
    public IActionResult GetApiTab([FromQuery] int hours = 24)
    {
        _logger.LogDebug("Loading API tab content for {Hours} hours", hours);

        try
        {
            var viewModel = _aggregator.BuildApiRateLimits(hours);
            return PartialView("~/Pages/Admin/Performance/Tabs/_ApiTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load API tab content");
            return StatusCode(500, CreateErrorHtml("Failed to load API metrics"));
        }
    }

    /// <summary>
    /// Gets the System Health tab content.
    /// </summary>
    /// <param name="hours">Time range in hours (24, 168, or 720).</param>
    /// <returns>Partial view HTML for the system tab.</returns>
    [HttpGet("system")]
    [Produces("text/html")]
    public IActionResult GetSystemTab([FromQuery] int hours = 24)
    {
        _logger.LogDebug("Loading System tab content for {Hours} hours", hours);

        try
        {
            var viewModel = _aggregator.BuildSystemHealth();
            return PartialView("~/Pages/Admin/Performance/Tabs/_SystemTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load System tab content");
            return StatusCode(500, CreateErrorHtml("Failed to load system health data"));
        }
    }

    /// <summary>
    /// Gets the Alerts tab content.
    /// </summary>
    /// <param name="hours">Time range in hours (24, 168, or 720).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Partial view HTML for the alerts tab.</returns>
    [HttpGet("alerts")]
    [Produces("text/html")]
    public async Task<IActionResult> GetAlertsTab([FromQuery] int hours = 24, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading Alerts tab content for {Hours} hours", hours);

        try
        {
            var viewModel = await _aggregator.BuildAlertsPageAsync(User, cancellationToken);
            return PartialView("~/Pages/Admin/Performance/Tabs/_AlertsTab.cshtml", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Alerts tab content");
            return StatusCode(500, CreateErrorHtml("Failed to load alerts data"));
        }
    }

    #region Helpers

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
        <button class=""btn btn-secondary tab-retry-btn"" onclick=""window.PerformanceTabs?.retryCurrentTab()"">
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
