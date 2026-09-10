using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Page model for the API Rate Limits and Metrics page.
/// Displays Discord API usage, rate limit status, and latency tracking.
/// All data aggregation is delegated to <see cref="IPerformanceDashboardAggregator"/>.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class ApiMetricsModel : PageModel
{
    private readonly IPerformanceDashboardAggregator _aggregator;
    private readonly ILogger<ApiMetricsModel> _logger;

    /// <summary>
    /// Gets the view model for the API metrics page.
    /// </summary>
    public ApiRateLimitsViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Gets or sets the number of hours of history to display (24, 168, or 720).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int Hours { get; set; } = 24;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiMetricsModel"/> class.
    /// </summary>
    /// <param name="aggregator">The performance dashboard aggregator.</param>
    /// <param name="logger">The logger.</param>
    public ApiMetricsModel(
        IPerformanceDashboardAggregator aggregator,
        ILogger<ApiMetricsModel> logger)
    {
        _aggregator = aggregator;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the API Metrics page.
    /// </summary>
    public Task OnGetAsync()
    {
        _logger.LogDebug("API Metrics page accessed by user {UserId}, hours={Hours}",
            User.Identity?.Name, Hours);

        ViewModel = _aggregator.BuildApiRateLimits(Hours);
        return Task.CompletedTask;
    }
}
