using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for connection event persistence.
/// </summary>
public interface IConnectionEventRepository : IRepository<ConnectionEvent>
{
    /// <summary>
    /// Gets all connection events since the specified timestamp.
    /// </summary>
    /// <param name="since">The start timestamp (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Connection events ordered by timestamp ascending.</returns>
    Task<IReadOnlyList<ConnectionEvent>> GetEventsSinceAsync(
        DateTime since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent connection event.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The last event, or null if no events exist.</returns>
    Task<ConnectionEvent?> GetLastEventAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new connection event.
    /// </summary>
    /// <param name="eventType">Type of event ("Connected" or "Disconnected").</param>
    /// <param name="timestamp">When the event occurred (UTC).</param>
    /// <param name="reason">Optional reason for the event.</param>
    /// <param name="details">Optional additional details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created event.</returns>
    Task<ConnectionEvent> AddEventAsync(
        string eventType,
        DateTime timestamp,
        string? reason = null,
        string? details = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes connection events older than the specified retention period.
    /// </summary>
    /// <param name="retentionDays">Number of days to retain events.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of events deleted.</returns>
    Task<int> CleanupOldEventsAsync(int retentionDays, CancellationToken cancellationToken = default);
}
