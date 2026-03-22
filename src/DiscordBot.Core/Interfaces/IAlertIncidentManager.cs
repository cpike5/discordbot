using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Manages the lifecycle of performance alert incidents.
/// Handles creating new incidents when thresholds are breached, resolving incidents
/// when metrics return to normal, and sending associated notifications.
/// </summary>
public interface IAlertIncidentManager
{
    /// <summary>
    /// Handles a threshold breach by creating a new incident if one does not already exist
    /// for the given metric, broadcasting it via SignalR, and sending admin notifications.
    /// </summary>
    /// <param name="config">The alert configuration that was breached.</param>
    /// <param name="currentValue">The current metric value that caused the breach.</param>
    /// <param name="severity">The severity level of the breach.</param>
    /// <param name="threshold">The threshold value that was exceeded.</param>
    /// <param name="repository">The scoped alert repository for data access.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a new incident was created; false if an active incident already existed.</returns>
    Task<bool> HandleBreachAsync(
        PerformanceAlertConfig config,
        double currentValue,
        AlertSeverity severity,
        double threshold,
        IPerformanceAlertRepository repository,
        CancellationToken cancellationToken);

    /// <summary>
    /// Handles a normal (non-breaching) reading by auto-resolving any active incident
    /// for the given metric, broadcasting the resolution via SignalR, and sending admin notifications.
    /// </summary>
    /// <param name="config">The alert configuration for the metric.</param>
    /// <param name="repository">The scoped alert repository for data access.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if an incident was resolved; false if no active incident existed.</returns>
    Task<bool> HandleResolutionAsync(
        PerformanceAlertConfig config,
        IPerformanceAlertRepository repository,
        CancellationToken cancellationToken);
}
