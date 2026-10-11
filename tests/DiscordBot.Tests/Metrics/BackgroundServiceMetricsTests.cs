using DiscordBot.Bot.Metrics;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Moq;

namespace DiscordBot.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="BackgroundServiceMetrics"/>.
/// </summary>
public class BackgroundServiceMetricsTests : IDisposable
{
    private readonly SimpleMeterFactory _meterFactory = new();
    private readonly Mock<IBackgroundServiceHealthRegistry> _registry = new();
    private readonly BackgroundServiceMetrics _metrics;

    public BackgroundServiceMetricsTests()
    {
        _metrics = new BackgroundServiceMetrics(_meterFactory, _registry.Object);
    }

    private MetricCollector<T> Collect<T>(string instrument) where T : struct
        => new(_meterFactory.GetMeter(BackgroundServiceMetrics.MeterName)!, instrument);

    [Fact]
    public void RecordHeartbeat_TagsService()
    {
        using var collector = Collect<long>("discordbot.background.heartbeats");

        _metrics.RecordHeartbeat("ReminderExecutionService");

        collector.LastMeasurement!.Tags["service"].Should().Be("ReminderExecutionService");
    }

    [Fact]
    public void RecordError_TagsFatal()
    {
        using var collector = Collect<long>("discordbot.background.errors");

        _metrics.RecordError("MemberSyncService", fatal: true);

        collector.LastMeasurement!.Tags["fatal"].Should().Be("true");
    }

    [Fact]
    public void UpGauge_IsZeroOnlyForServicesInError()
    {
        _registry.Setup(r => r.GetAllHealth()).Returns(new List<BackgroundServiceHealthDto>
        {
            new() { ServiceName = "A", Status = "Running" },
            new() { ServiceName = "B", Status = "Error" },
            new() { ServiceName = "C", Status = "Syncing" }
        });
        using var collector = Collect<int>("discordbot.background.up");

        collector.RecordObservableInstruments();

        var byService = collector.GetMeasurementSnapshot()
            .ToDictionary(m => (string)m.Tags["service"]!, m => m.Value);
        byService.Should().BeEquivalentTo(new Dictionary<string, int> { ["A"] = 1, ["B"] = 0, ["C"] = 1 });
    }

    [Fact]
    public void HeartbeatAgeGauge_SkipsServicesWithoutHeartbeat()
    {
        _registry.Setup(r => r.GetAllHealth()).Returns(new List<BackgroundServiceHealthDto>
        {
            new() { ServiceName = "A", Status = "Running", LastHeartbeat = DateTime.UtcNow.AddSeconds(-30) },
            new() { ServiceName = "B", Status = "Running", LastHeartbeat = null }
        });
        using var collector = Collect<double>("discordbot.background.heartbeat.age");

        collector.RecordObservableInstruments();

        var measurement = collector.GetMeasurementSnapshot().Should().ContainSingle().Subject;
        measurement.Tags["service"].Should().Be("A");
        measurement.Value.Should().BeInRange(29, 60);
    }

    [Fact]
    public void Gauges_WithoutRegistry_ProduceNothing()
    {
        using var metrics = new BackgroundServiceMetrics(new SimpleMeterFactory());

        var act = () => metrics.RecordHeartbeat("A");

        act.Should().NotThrow();
    }

    public void Dispose() => _metrics.Dispose();
}
