using DiscordBot.Bot.Hubs;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Implementation of <see cref="IPerformanceNotifier"/> that broadcasts performance alert events via SignalR.
/// Handles real-time notifications for alert triggers, resolutions, acknowledgments, and active alert count changes.
/// </summary>
public class PerformanceNotifier : IPerformanceNotifier
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PerformanceNotifier> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PerformanceNotifier"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context for broadcasting to clients.</param>
    /// <param name="serviceProvider">Service provider for creating scopes to resolve scoped dependencies.</param>
    /// <param name="logger">Logger for diagnostic and error information.</param>
    public PerformanceNotifier(
        IHubContext<DashboardHub> hubContext,
        IServiceProvider serviceProvider,
        ILogger<PerformanceNotifier> logger)
    {
        _hubContext = hubContext;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task BroadcastAlertTriggeredAsync(PerformanceIncidentDto incident, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Broadcasting alert triggered event: IncidentId={IncidentId}, MetricName={MetricName}, Severity={Severity}",
                incident.Id,
                incident.MetricName,
                incident.Severity);

            await _hubContext.Clients
                .Group(DashboardHub.AlertsGroupName)
                .SendAsync("OnAlertTriggered", incident, cancellationToken);

            // Also broadcast the updated active alert count
            await BroadcastActiveAlertCountAsync(cancellationToken);

            _logger.LogTrace("Alert triggered event broadcast completed: IncidentId={IncidentId}", incident.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error broadcasting alert triggered event: IncidentId={IncidentId}",
                incident.Id);
        }
    }

    /// <inheritdoc/>
    public async Task BroadcastAlertResolvedAsync(PerformanceIncidentDto incident, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Broadcasting alert resolved event: IncidentId={IncidentId}, MetricName={MetricName}",
                incident.Id,
                incident.MetricName);

            await _hubContext.Clients
                .Group(DashboardHub.AlertsGroupName)
                .SendAsync("OnAlertResolved", incident, cancellationToken);

            // Also broadcast the updated active alert count
            await BroadcastActiveAlertCountAsync(cancellationToken);

            _logger.LogTrace("Alert resolved event broadcast completed: IncidentId={IncidentId}", incident.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error broadcasting alert resolved event: IncidentId={IncidentId}",
                incident.Id);
        }
    }

    /// <inheritdoc/>
    public async Task BroadcastAlertAcknowledgedAsync(Guid incidentId, string acknowledgedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Broadcasting alert acknowledged event: IncidentId={IncidentId}, AcknowledgedBy={AcknowledgedBy}",
                incidentId,
                acknowledgedBy);

            var payload = new
            {
                IncidentId = incidentId,
                AcknowledgedBy = acknowledgedBy,
                AcknowledgedAt = DateTime.UtcNow
            };

            await _hubContext.Clients
                .Group(DashboardHub.AlertsGroupName)
                .SendAsync("OnAlertAcknowledged", payload, cancellationToken);

            _logger.LogTrace("Alert acknowledged event broadcast completed: IncidentId={IncidentId}", incidentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error broadcasting alert acknowledged event: IncidentId={IncidentId}",
                incidentId);
        }
    }

    /// <inheritdoc/>
    public async Task BroadcastActiveAlertCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogTrace("Broadcasting active alert count");

            // Create a scope to resolve the scoped repository
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IPerformanceAlertRepository>();

            var activeIncidents = await repository.GetActiveIncidentsAsync(cancellationToken);

            var summary = new ActiveAlertSummaryDto
            {
                ActiveCount = activeIncidents.Count,
                CriticalCount = activeIncidents.Count(i => i.Severity == AlertSeverity.Critical),
                WarningCount = activeIncidents.Count(i => i.Severity == AlertSeverity.Warning),
                InfoCount = activeIncidents.Count(i => i.Severity == AlertSeverity.Info)
            };

            await _hubContext.Clients
                .Group(DashboardHub.AlertsGroupName)
                .SendAsync("OnActiveAlertCountChanged", summary, cancellationToken);

            _logger.LogTrace(
                "Active alert count broadcast completed: ActiveCount={ActiveCount}, Critical={CriticalCount}, Warning={WarningCount}",
                summary.ActiveCount,
                summary.CriticalCount,
                summary.WarningCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error broadcasting active alert count");
        }
    }
}
