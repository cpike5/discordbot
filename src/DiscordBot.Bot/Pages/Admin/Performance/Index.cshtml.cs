using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Performance;
using DiscordBot.Bot.ViewModels.Pages;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Page model for the Performance Overview dashboard.
/// Displays aggregated performance metrics, system health, and active alerts.
/// Uses a shell layout with client-side tab switching. All data aggregation is delegated
/// to <see cref="IPerformanceDashboardAggregator"/>; this page model only routes requests
/// to the right tab builder and returns the matching partial view.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class IndexModel : PageModel
{
    private readonly IPerformanceDashboardAggregator _aggregator;
    private readonly ILogger<IndexModel> _logger;

    /// <summary>
    /// Gets the view model for the performance overview page content.
    /// </summary>
    public PerformanceOverviewViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Gets the shell view model for the performance dashboard layout.
    /// </summary>
    public PerformanceShellViewModel ShellViewModel { get; private set; } = new();

    /// <summary>
    /// Gets or sets the tab to open, from <c>?tab=</c>. Unknown values fall back to Overview.
    /// This is the one place a tab is addressed; the retired standalone pages redirect here.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "tab")]
    public string? Tab { get; set; }

    /// <summary>
    /// Gets or sets the time range in hours, from <c>?hours=</c>. Clamped to 24, 168 or 720; when
    /// absent the script falls back to the range the viewer last chose.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "hours")]
    public int? Hours { get; set; }

    /// <summary>
    /// Gets the tab id to show first (always a known tab).
    /// </summary>
    public string ActiveTabId => PerformanceDashboardTabs.NormalizeTab(Tab);

    /// <summary>
    /// Gets the time range the URL asked for, clamped, or null when it did not ask.
    /// </summary>
    public int? RequestedHours => Hours.HasValue ? PerformanceDashboardTabs.NormalizeHours(Hours.Value) : null;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    public IndexModel(
        IPerformanceDashboardAggregator aggregator,
        ILogger<IndexModel> logger)
    {
        _aggregator = aggregator;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the Performance Overview page.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Performance Overview page accessed by user {UserId}", User.Identity?.Name);
        await LoadViewModelAsync(PerformanceDashboardTabs.DefaultHours, cancellationToken);
    }

    /// <summary>
    /// Handles AJAX requests for tab content partial views.
    /// </summary>
    /// <param name="tabId">The ID of the tab to load.</param>
    /// <param name="hours">The time range in hours; clamped to 24, 168 or 720.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The partial view for the requested tab.</returns>
    public async Task<IActionResult> OnGetPartialAsync(string tabId, int hours = 24, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading partial content for tab {TabId} with hours={Hours}", tabId, hours);

        hours = PerformanceDashboardTabs.NormalizeHours(hours);

        return tabId?.ToLowerInvariant() switch
        {
            "overview" => await LoadOverviewTabAsync(hours, cancellationToken),
            "health" => await LoadHealthTabAsync(cancellationToken),
            "commands" => await LoadCommandsTabAsync(hours, cancellationToken),
            "api" => LoadApiTab(hours),
            "system" => LoadSystemTab(),
            "alerts" => await LoadAlertsTabAsync(cancellationToken),
            _ => HandleInvalidTab(tabId)
        };
    }

    private async Task<IActionResult> LoadOverviewTabAsync(int hours, CancellationToken cancellationToken)
    {
        await LoadViewModelAsync(hours, cancellationToken);
        return Partial("Tabs/_OverviewTab", ViewModel);
    }

    private async Task<IActionResult> LoadHealthTabAsync(CancellationToken cancellationToken)
    {
        var viewModel = await _aggregator.BuildHealthMetricsAsync(cancellationToken);
        return Partial("Tabs/_HealthTab", viewModel);
    }

    private async Task<IActionResult> LoadCommandsTabAsync(int hours, CancellationToken cancellationToken)
    {
        var viewModel = await _aggregator.BuildCommandPerformanceAsync(hours, cancellationToken);
        return Partial("Tabs/_CommandsTab", viewModel);
    }

    private IActionResult LoadApiTab(int hours)
    {
        var viewModel = _aggregator.BuildApiRateLimits(hours);
        return Partial("Tabs/_ApiTab", viewModel);
    }

    private IActionResult LoadSystemTab()
    {
        var viewModel = _aggregator.BuildSystemHealth();
        return Partial("Tabs/_SystemTab", viewModel);
    }

    private async Task<IActionResult> LoadAlertsTabAsync(CancellationToken cancellationToken)
    {
        var viewModel = await _aggregator.BuildAlertsPageAsync(User, cancellationToken);
        return Partial("Tabs/_AlertsTab", viewModel);
    }

    private IActionResult HandleInvalidTab(string? tabId)
    {
        _logger.LogWarning("Invalid tab ID requested: {TabId}", tabId);
        return NotFound();
    }

    private async Task LoadViewModelAsync(int hours, CancellationToken cancellationToken)
    {
        var result = await _aggregator.BuildOverviewAsync(hours, cancellationToken);
        ViewModel = result.Overview;
        ShellViewModel = result.Shell;
    }
}
