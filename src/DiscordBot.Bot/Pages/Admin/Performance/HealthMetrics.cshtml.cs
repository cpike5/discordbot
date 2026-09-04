using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Page model for the Bot Health Metrics dashboard.
/// Displays connection status, uptime, latency, and system resource metrics.
/// All data aggregation is delegated to <see cref="IPerformanceDashboardAggregator"/>.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class HealthMetricsModel : PageModel
{
    private readonly IPerformanceDashboardAggregator _aggregator;
    private readonly ILogger<HealthMetricsModel> _logger;

    /// <summary>
    /// Gets the view model for the health metrics page.
    /// </summary>
    public HealthMetricsViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthMetricsModel"/> class.
    /// </summary>
    /// <param name="aggregator">The performance dashboard aggregator.</param>
    /// <param name="logger">The logger.</param>
    public HealthMetricsModel(
        IPerformanceDashboardAggregator aggregator,
        ILogger<HealthMetricsModel> logger)
    {
        _aggregator = aggregator;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the Health Metrics page.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Health Metrics page accessed by user {UserId}", User.Identity?.Name);
        ViewModel = await _aggregator.BuildHealthMetricsAsync(cancellationToken);
    }
}
