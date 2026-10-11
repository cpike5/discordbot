using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DiscordBot.Bot.Health;

/// <summary>
/// Publishes the registered health checks as metrics, so a failing check shows up in Grafana and
/// can alert, rather than only when something polls <c>/health</c>.
/// </summary>
/// <remarks>
/// The health check service runs every registered check on a timer and hands the report here
/// (period set in <c>HealthCheckExtensions</c>). The last result per check is exposed as gauges.
/// </remarks>
public sealed class HealthCheckMetricsPublisher : IHealthCheckPublisher, IDisposable
{
    public const string MeterName = "DiscordBot.Health";

    private readonly Meter _meter;
    private readonly ConcurrentDictionary<string, (double Status, double DurationMs)> _lastResults = new();

    public HealthCheckMetricsPublisher(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _meter.CreateObservableGauge(
            name: "discordbot.health.status",
            observeValues: () => Observe(r => r.Status),
            unit: "{state}",
            description: "Last health check result: 1 healthy, 0.5 degraded, 0 unhealthy");

        _meter.CreateObservableGauge(
            name: "discordbot.health.check.duration",
            observeValues: () => Observe(r => r.DurationMs),
            unit: "ms",
            description: "Duration of the last run of each health check");
    }

    /// <inheritdoc />
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        foreach (var (name, entry) in report.Entries)
        {
            _lastResults[name] = (ToValue(entry.Status), entry.Duration.TotalMilliseconds);
        }

        _lastResults["overall"] = (ToValue(report.Status), report.TotalDuration.TotalMilliseconds);
        return Task.CompletedTask;
    }

    /// <summary>Maps a health status to its gauge value.</summary>
    public static double ToValue(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => 1,
        HealthStatus.Degraded => 0.5,
        _ => 0
    };

    private IEnumerable<Measurement<double>> Observe(Func<(double Status, double DurationMs), double> select)
    {
        foreach (var (name, result) in _lastResults)
        {
            yield return new Measurement<double>(select(result), new KeyValuePair<string, object?>("check", name));
        }
    }

    public void Dispose() => _meter.Dispose();
}
