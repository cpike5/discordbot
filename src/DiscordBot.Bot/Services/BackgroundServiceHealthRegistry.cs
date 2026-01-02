using System.Collections.Concurrent;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Registry for tracking health status of background services.
/// Provides aggregated health reporting across all registered services.
/// Thread-safe singleton service using concurrent dictionary.
/// </summary>
public class BackgroundServiceHealthRegistry : IBackgroundServiceHealthRegistry
{
    private readonly ILogger<BackgroundServiceHealthRegistry> _logger;
    private readonly ConcurrentDictionary<string, IBackgroundServiceHealth> _services = new();

    public BackgroundServiceHealthRegistry(ILogger<BackgroundServiceHealthRegistry> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void Register(string name, IBackgroundServiceHealth service)
    {
        if (_services.TryAdd(name, service))
        {
            _logger.LogInformation("Registered background service for health monitoring: {ServiceName}", name);
        }
        else
        {
            _logger.LogWarning("Background service already registered: {ServiceName}", name);
        }
    }

    /// <inheritdoc/>
    public void Unregister(string name)
    {
        if (_services.TryRemove(name, out _))
        {
            _logger.LogInformation("Unregistered background service from health monitoring: {ServiceName}", name);
        }
        else
        {
            _logger.LogWarning("Attempted to unregister non-existent service: {ServiceName}", name);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<BackgroundServiceHealthDto> GetAllHealth()
    {
        return _services.Select(kvp => new BackgroundServiceHealthDto
        {
            ServiceName = kvp.Value.ServiceName,
            Status = kvp.Value.Status,
            LastHeartbeat = kvp.Value.LastHeartbeat,
            LastError = kvp.Value.LastError
        }).ToList();
    }

    /// <inheritdoc/>
    public BackgroundServiceHealthDto? GetHealth(string serviceName)
    {
        if (_services.TryGetValue(serviceName, out var service))
        {
            return new BackgroundServiceHealthDto
            {
                ServiceName = service.ServiceName,
                Status = service.Status,
                LastHeartbeat = service.LastHeartbeat,
                LastError = service.LastError
            };
        }

        return null;
    }

    /// <inheritdoc/>
    public string GetOverallStatus()
    {
        if (_services.IsEmpty)
        {
            return "Unknown";
        }

        var statuses = _services.Values.Select(s => s.Status).ToList();

        // If any service has an error, overall is unhealthy
        if (statuses.Any(s => s.Equals("Error", StringComparison.OrdinalIgnoreCase)))
        {
            return "Unhealthy";
        }

        // If any service is stopped, overall is degraded
        if (statuses.Any(s => s.Equals("Stopped", StringComparison.OrdinalIgnoreCase)))
        {
            return "Degraded";
        }

        // Check for stale heartbeats (no heartbeat in last 5 minutes)
        var now = DateTime.UtcNow;
        var staleThreshold = TimeSpan.FromMinutes(5);

        foreach (var service in _services.Values)
        {
            if (service.LastHeartbeat.HasValue)
            {
                var timeSinceHeartbeat = now - service.LastHeartbeat.Value;
                if (timeSinceHeartbeat > staleThreshold)
                {
                    _logger.LogDebug(
                        "Service {ServiceName} has stale heartbeat: {TimeSinceHeartbeat}",
                        service.ServiceName, timeSinceHeartbeat);
                    return "Degraded";
                }
            }
        }

        // All services running normally
        return "Healthy";
    }
}
