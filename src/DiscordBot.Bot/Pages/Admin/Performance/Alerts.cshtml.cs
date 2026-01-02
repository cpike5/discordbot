using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        await LoadViewModelAsync(cancellationToken);
    }

    /// <summary>
    /// Handles POST requests to acknowledge a single incident.
    /// Requires Admin role or higher.
    /// </summary>
    /// <param name="id">The incident ID to acknowledge.</param>
    /// <param name="notes">Optional notes about the acknowledgment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the same page.</returns>
    public async Task<IActionResult> OnPostAcknowledgeAsync(Guid id, string? notes, CancellationToken cancellationToken = default)
    {
        // Check admin authorization
        var authResult = await _authorizationService.AuthorizeAsync(User, "RequireAdmin");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        try
        {
            var userId = User.Identity?.Name ?? "Unknown";
            await _alertService.AcknowledgeIncidentAsync(id, userId, notes, cancellationToken);

            _logger.LogInformation(
                "Incident {IncidentId} acknowledged by user {UserId}",
                id,
                userId);

            TempData["SuccessMessage"] = "Incident acknowledged successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to acknowledge incident {IncidentId} by user {UserId}",
                id,
                User.Identity?.Name);

            TempData["ErrorMessage"] = "Failed to acknowledge incident. Please try again.";
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Handles POST requests to acknowledge all active incidents.
    /// Requires Admin role or higher.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the same page.</returns>
    public async Task<IActionResult> OnPostAcknowledgeAllAsync(CancellationToken cancellationToken = default)
    {
        // Check admin authorization
        var authResult = await _authorizationService.AuthorizeAsync(User, "RequireAdmin");
        if (!authResult.Succeeded)
        {
            return Forbid();
        }

        try
        {
            var userId = User.Identity?.Name ?? "Unknown";
            var count = await _alertService.AcknowledgeAllActiveAsync(userId, cancellationToken);

            _logger.LogInformation(
                "Acknowledged {Count} active incidents by user {UserId}",
                count,
                userId);

            TempData["SuccessMessage"] = $"Acknowledged {count} active incident{(count != 1 ? "s" : "")} successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to acknowledge all active incidents by user {UserId}",
                User.Identity?.Name);

            TempData["ErrorMessage"] = "Failed to acknowledge incidents. Please try again.";
        }

        return RedirectToPage();
    }

    private async Task LoadViewModelAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Fetch all data in parallel for better performance
            var activeIncidentsTask = _alertService.GetActiveIncidentsAsync(cancellationToken);
            var alertConfigsTask = _alertService.GetAllConfigsAsync(cancellationToken);
            var recentIncidentsTask = _alertService.GetIncidentHistoryAsync(
                new IncidentQueryDto { PageNumber = 1, PageSize = 10 },
                cancellationToken);
            var autoRecoveryEventsTask = _alertService.GetAutoRecoveryEventsAsync(10, cancellationToken);
            var alertFrequencyTask = _alertService.GetAlertFrequencyDataAsync(30, cancellationToken);
            var summaryTask = _alertService.GetActiveAlertSummaryAsync(cancellationToken);

            await Task.WhenAll(
                activeIncidentsTask,
                alertConfigsTask,
                recentIncidentsTask,
                autoRecoveryEventsTask,
                alertFrequencyTask,
                summaryTask);

            ViewModel = new AlertsPageViewModel
            {
                ActiveIncidents = activeIncidentsTask.Result,
                AlertConfigs = alertConfigsTask.Result,
                RecentIncidents = recentIncidentsTask.Result.Items,
                AutoRecoveryEvents = autoRecoveryEventsTask.Result,
                AlertFrequencyData = alertFrequencyTask.Result,
                AlertSummary = summaryTask.Result
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
