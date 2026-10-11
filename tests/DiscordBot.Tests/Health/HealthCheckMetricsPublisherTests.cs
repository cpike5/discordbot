using DiscordBot.Bot.Health;
using DiscordBot.Tests.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace DiscordBot.Tests.Health;

/// <summary>
/// Unit tests for <see cref="HealthCheckMetricsPublisher"/>.
/// </summary>
public class HealthCheckMetricsPublisherTests : IDisposable
{
    private readonly SimpleMeterFactory _meterFactory = new();
    private readonly HealthCheckMetricsPublisher _publisher;

    public HealthCheckMetricsPublisherTests()
    {
        _publisher = new HealthCheckMetricsPublisher(_meterFactory);
    }

    [Theory]
    [InlineData(HealthStatus.Healthy, 1)]
    [InlineData(HealthStatus.Degraded, 0.5)]
    [InlineData(HealthStatus.Unhealthy, 0)]
    public void ToValue_MapsStatus(HealthStatus status, double expected)
    {
        HealthCheckMetricsPublisher.ToValue(status).Should().Be(expected);
    }

    [Fact]
    public async Task PublishAsync_ExposesEachCheckAndOverall()
    {
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>
            {
                ["database"] = new(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(12), null, null),
                ["discord"] = new(HealthStatus.Degraded, null, TimeSpan.FromMilliseconds(1), null, null)
            },
            TimeSpan.FromMilliseconds(13));
        using var collector = new MetricCollector<double>(
            _meterFactory.GetMeter(HealthCheckMetricsPublisher.MeterName)!, "discordbot.health.status");

        await _publisher.PublishAsync(report, CancellationToken.None);
        collector.RecordObservableInstruments();

        var byCheck = collector.GetMeasurementSnapshot()
            .ToDictionary(m => (string)m.Tags["check"]!, m => m.Value);
        byCheck.Should().BeEquivalentTo(new Dictionary<string, double>
        {
            ["database"] = 1,
            ["discord"] = 0.5,
            ["overall"] = 0.5
        });
    }

    public void Dispose() => _publisher.Dispose();
}
