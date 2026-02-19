using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Core.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Pages.Admin.Performance;

/// <summary>
/// Page model for the Performance Alerts &amp; Incidents page.
/// Displays active alerts, incident history, auto-recovery events, and alert configuration.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class AlertsModel : PageModel
{
    private readonly IPerformanceAlertService _alertService;
    private readonly IAuthorizationService _authorizationService;
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
    /// <param name="alertService">The performance alert service.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="logger">The logger.</param>
    public AlertsModel(
        IPerformanceAlertService alertService,
        IAuthorizationService authorizationService,
        ILogger<AlertsModel> logger)
    {
        _alertService = alertService;
        _authorizationService = authorizationService;
        _logger = logger;
    }

    /// <summary>
    /// Handles GET requests for the Alerts page.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task OnGetAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Alerts page accessed by user {UserId}", User.Identity?.Name);

        // Check if user has Admin permission using policy-based authorization
        var authResult = await _authorizationService.AuthorizeAsync(User, "RequireAdmin");
        CanEdit = authResult.Succeeded;

        await LoadViewModelAsync(cancellationToken);
    }

    private async Task LoadViewModelAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Execute sequentially — DbContext is not thread-safe
            var activeIncidents = await _alertService.GetActiveIncidentsAsync(cancellationToken);
            var alertConfigs = await _alertService.GetAllConfigsAsync(cancellationToken);
            var recentIncidents = await _alertService.GetIncidentHistoryAsync(
                new IncidentQueryDto { PageNumber = 1, PageSize = 10 },
                cancellationToken);
            var autoRecoveryEvents = await _alertService.GetAutoRecoveryEventsAsync(10, cancellationToken);
            var alertFrequency = await _alertService.GetAlertFrequencyDataAsync(30, cancellationToken);
            var alertSummary = await _alertService.GetActiveAlertSummaryAsync(cancellationToken);

            ViewModel = new AlertsPageViewModel
            {
                ActiveIncidents = activeIncidents,
                AlertConfigs = alertConfigs,
                RecentIncidents = recentIncidents.Items,
                AutoRecoveryEvents = autoRecoveryEvents,
                AlertFrequencyData = alertFrequency,
                AlertSummary = alertSummary,
                CanEdit = CanEdit
            };

            _logger.LogDebug(
                "Alerts ViewModel loaded: ActiveIncidents={ActiveCount}, TotalConfigs={ConfigCount}, RecentIncidents={RecentCount}",
                ViewModel.ActiveIncidents.Count,
                ViewModel.AlertConfigs.Count,
                ViewModel.RecentIncidents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Alerts ViewModel");

            // Create a default view model in case of error
            ViewModel = new AlertsPageViewModel();
            TempData["ErrorMessage"] = "Failed to load alerts data. Please try again.";
        }
    }
}
