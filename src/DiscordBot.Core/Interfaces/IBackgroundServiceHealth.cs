namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Interface that background services can implement to report their health status.
/// </summary>
public interface IBackgroundServiceHealth
{
    /// <summary>
    /// Gets the unique name of the background service.
    /// </summary>
    string ServiceName { get; }

    /// <summary>
    /// Gets the current status of the service (e.g., "Running", "Stopped", "Error").
    /// </summary>
    string Status { get; }

    /// <summary>
    /// Gets the timestamp of the last heartbeat or activity (UTC).
    /// </summary>
    DateTime? LastHeartbeat { get; }

    /// <summary>
    /// Gets the last error message, if any.
    /// </summary>
    string? LastError { get; }
}
