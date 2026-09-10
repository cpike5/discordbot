using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Page model for the Performance Alerts &amp; Incidents page.
/// Displays active alerts, incident history, auto-recovery events, and alert configuration.
/// All data aggregation is delegated to <see cref="IPerformanceDashboardAggregator"/>.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class AlertsModel : PageModel
{
    private readonly IPerformanceDashboardAggregator _aggregator;
    private readonly ILogger<AlertsModel> _logger;

    /// <summary>
    /// Gets the view model for the alerts page.
    /// </summary>
    public AlertsPageViewModel ViewModel { get; private set; } = new();

    /// <summary>
    /// Gets a value indicating whether the current user can edit alert settings.
    /// Only Admin and SuperAdmin roles can modify alert configurations.
    /// </summary>
    public bool CanEdit { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlertsModel"/> class.
    /// </summary>
    /// <param name="aggregator">The performance dashboard aggregator.</param>
    /// <param name="logger">The logger.</param>
    public AlertsModel(
        IPerformanceDashboardAggregator aggregator,
        ILogger<AlertsModel> logger)
    {
        _aggregator = aggregator;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the Alerts page.
    /// </summary>
    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Alerts page accessed by user {UserId}", User.Identity?.Name);

        ViewModel = await _aggregator.BuildAlertsPageAsync(User, cancellationToken);
        CanEdit = ViewModel.CanEdit;

        if (ViewModel.LoadFailed)
        {
            TempData["ErrorMessage"] = "Failed to load alerts data. Please try again.";
        }
    }
}
