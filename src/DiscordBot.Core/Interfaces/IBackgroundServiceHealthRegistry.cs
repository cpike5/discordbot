using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for registering and querying health status of background services.
/// </summary>
public interface IBackgroundServiceHealthRegistry
{
    /// <summary>
    /// Registers a background service for health monitoring.
    /// </summary>
    /// <param name="name">The unique name of the service.</param>
    /// <param name="service">The service instance that implements IBackgroundServiceHealth.</param>
    void Register(string name, IBackgroundServiceHealth service);

    /// <summary>
    /// Unregisters a background service from health monitoring.
    /// </summary>
    /// <param name="name">The unique name of the service to unregister.</param>
    void Unregister(string name);

    /// <summary>
    /// Gets health status for all registered background services.
    /// </summary>
    /// <returns>A read-only list of background service health information.</returns>
    IReadOnlyList<BackgroundServiceHealthDto> GetAllHealth();

    /// <summary>
    /// Gets health status for a specific background service.
    /// </summary>
    /// <param name="serviceName">The unique name of the service.</param>
    /// <returns>The health information for the service, or null if not registered.</returns>
    BackgroundServiceHealthDto? GetHealth(string serviceName);

    /// <summary>
    /// Gets the overall health status across all registered services.
    /// </summary>
    /// <returns>Overall status: "Healthy", "Degraded", or "Unhealthy".</returns>
    string GetOverallStatus();
}
