using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Page model for the System Health dashboard.
/// Displays database performance, background services, cache statistics, and memory metrics.
/// All data aggregation is delegated to <see cref="IPerformanceDashboardAggregator"/>.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class SystemHealthModel : PageModel
{
    private readonly IPerformanceDashboardAggregator _aggregator;
    private readonly ILogger<SystemHealthModel> _logger;

    /// <summary>
    /// Gets the view model for the system health page.
    /// </summary>
    public SystemHealthViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemHealthModel"/> class.
    /// </summary>
    /// <param name="aggregator">The performance dashboard aggregator.</param>
    /// <param name="logger">The logger.</param>
    public SystemHealthModel(
        IPerformanceDashboardAggregator aggregator,
        ILogger<SystemHealthModel> logger)
    {
        _aggregator = aggregator;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the System Health page.
    /// </summary>
    public void OnGet()
    {
        _logger.LogDebug("System Health page accessed by user {UserId}", User.Identity?.Name);
        ViewModel = _aggregator.BuildSystemHealth();
    }
}
