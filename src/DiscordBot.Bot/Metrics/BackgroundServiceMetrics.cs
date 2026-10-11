using System.Diagnostics;
using System.Diagnostics.Metrics;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Metrics for <see cref="Services.MonitoredBackgroundService"/> subclasses: heartbeats, recorded
/// errors, and the per-service status and heartbeat age the health registry already tracks.
/// </summary>
/// <remarks>
/// The counters are recorded by the base class itself, so every monitored service is covered
/// without touching the subclasses. The gauges read <see cref="IBackgroundServiceHealthRegistry"/>
/// on each collection.
/// </remarks>
public sealed class BackgroundServiceMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.BackgroundServices";

    private readonly Meter _meter;
    private readonly Counter<long> _heartbeats;
    private readonly Counter<long> _errors;
    private readonly IBackgroundServiceHealthRegistry? _registry;

    public BackgroundServiceMetrics(IMeterFactory meterFactory, IBackgroundServiceHealthRegistry? registry = null)
    {
        _registry = registry;
        _meter = meterFactory.Create(MeterName);

        _heartbeats = _meter.CreateCounter<long>(
            name: "discordbot.background.heartbeats",
            unit: "{heartbeats}",
            description: "Background service heartbeats (one per completed work cycle)");

        _errors = _meter.CreateCounter<long>(
            name: "discordbot.background.errors",
            unit: "{errors}",
            description: "Errors recorded by background services, including fatal ones");

        _meter.CreateObservableGauge(
            name: "discordbot.background.up",
            observeValues: ObserveStatus,
            unit: "{state}",
            description: "1 when a background service is healthy, 0 when it reports an error; tagged with its status");

        _meter.CreateObservableGauge(
            name: "discordbot.background.heartbeat.age",
            observeValues: ObserveHeartbeatAge,
            unit: "s",
            description: "Seconds since a background service's last heartbeat");
    }

    /// <summary>Records one heartbeat for a service.</summary>
    public void RecordHeartbeat(string serviceName)
        => _heartbeats.Add(1, new KeyValuePair<string, object?>("service", serviceName));

    /// <summary>Records one error for a service.</summary>
    /// <param name="serviceName">The service that recorded the error.</param>
    /// <param name="fatal">True when the error stopped the service.</param>
    public void RecordError(string serviceName, bool fatal = false)
    {
        _errors.Add(1, new TagList
        {
            { "service", serviceName },
            { "fatal", fatal ? "true" : "false" }
        });
    }

    private IEnumerable<Measurement<int>> ObserveStatus()
    {
        foreach (var health in _registry?.GetAllHealth() ?? [])
        {
            var up = string.Equals(health.Status, "Error", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            yield return new Measurement<int>(up, new TagList
            {
                { "service", health.ServiceName },
                { "status", health.Status }
            });
        }
    }

    private IEnumerable<Measurement<double>> ObserveHeartbeatAge()
    {
        var now = DateTime.UtcNow;
        foreach (var health in _registry?.GetAllHealth() ?? [])
        {
            if (health.LastHeartbeat is { } heartbeat)
            {
                yield return new Measurement<double>(
                    Math.Max(0, (now - heartbeat).TotalSeconds),
                    new KeyValuePair<string, object?>("service", health.ServiceName));
            }
        }
    }

    public void Dispose() => _meter.Dispose();
}
