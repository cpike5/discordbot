using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for broadcasting performance alert notifications via SignalR.
/// Provides methods for notifying connected clients about alert triggers, resolutions, and acknowledgments.
/// </summary>
public interface IPerformanceNotifier
{
    /// <summary>
    /// Broadcasts an alert triggered event to all subscribed clients.
    /// </summary>
    /// <param name="incident">The performance incident that was triggered.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastAlertTriggeredAsync(PerformanceIncidentDto incident, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts an alert resolved event to all subscribed clients.
    /// </summary>
    /// <param name="incident">The performance incident that was resolved.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastAlertResolvedAsync(PerformanceIncidentDto incident, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts an alert acknowledged event to all subscribed clients.
    /// </summary>
    /// <param name="incidentId">The incident ID that was acknowledged.</param>
    /// <param name="acknowledgedBy">The user ID of the administrator who acknowledged the alert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastAlertAcknowledgedAsync(Guid incidentId, string acknowledgedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts the current active alert count to all subscribed clients.
    /// Retrieves the latest active incident counts by severity and broadcasts the summary.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastActiveAlertCountAsync(CancellationToken cancellationToken = default);
}
