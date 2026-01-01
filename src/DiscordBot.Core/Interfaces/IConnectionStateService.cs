using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for tracking Discord gateway connection state changes and uptime.
/// </summary>
public interface IConnectionStateService
{
    /// <summary>
    /// Records a successful connection to the Discord gateway.
    /// </summary>
    void RecordConnected();

    /// <summary>
    /// Records a disconnection from the Discord gateway.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnection, if any.</param>
    void RecordDisconnected(Exception? exception);

    /// <summary>
    /// Gets the current connection state.
    /// </summary>
    /// <returns>The current connection state.</returns>
    GatewayConnectionState GetCurrentState();

    /// <summary>
    /// Gets the timestamp of the last successful connection (UTC).
    /// </summary>
    /// <returns>The last connection timestamp, or null if never connected.</returns>
    DateTime? GetLastConnectedTime();

    /// <summary>
    /// Gets the timestamp of the last disconnection (UTC).
    /// </summary>
    /// <returns>The last disconnection timestamp, or null if never disconnected.</returns>
    DateTime? GetLastDisconnectedTime();

    /// <summary>
    /// Gets the duration of the current connection session.
    /// </summary>
    /// <returns>The current session duration, or TimeSpan.Zero if not connected.</returns>
    TimeSpan GetCurrentSessionDuration();

    /// <summary>
    /// Calculates the uptime percentage over a specified time period.
    /// </summary>
    /// <param name="period">The time period to calculate uptime over.</param>
    /// <returns>The uptime percentage (0-100).</returns>
    double GetUptimePercentage(TimeSpan period);

    /// <summary>
    /// Gets the connection event history for a specified number of days.
    /// </summary>
    /// <param name="days">The number of days of history to retrieve (default: 7).</param>
    /// <returns>A read-only list of connection events.</returns>
    IReadOnlyList<ConnectionEventDto> GetConnectionEvents(int days = 7);

    /// <summary>
    /// Gets aggregate connection statistics for a specified number of days.
    /// </summary>
    /// <param name="days">The number of days to aggregate over (default: 7).</param>
    /// <returns>Connection statistics.</returns>
    ConnectionStatsDto GetConnectionStats(int days = 7);
}

/// <summary>
/// Discord gateway connection states.
/// </summary>
public enum GatewayConnectionState
{
    /// <summary>
    /// Bot is currently connected to the Discord gateway.
    /// </summary>
    Connected,

    /// <summary>
    /// Bot is disconnected from the Discord gateway.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Bot is in the process of connecting to the Discord gateway.
    /// </summary>
    Connecting
}
