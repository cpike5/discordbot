using System.Diagnostics.Metrics;
using DiscordBot.Bot.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace DiscordBot.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="SloMetrics"/>.
/// Tests verify that Service Level Objective (SLO) metrics are recorded correctly with appropriate values.
/// </summary>
public class SloMetricsTests : IDisposable
{
    private readonly SimpleMeterFactory _meterFactory;
    private readonly Meter _meter;
    private readonly SloMetrics _sloMetrics;
    private readonly MetricCollector<double> _commandSuccessRate24hCollector;
    private readonly MetricCollector<double> _apiSuccessRate24hCollector;
    private readonly MetricCollector<double> _commandP99Latency1hCollector;
    private readonly MetricCollector<double> _errorBudgetRemainingCollector;
    private readonly MetricCollector<double> _uptimePercentage30dCollector;

    public SloMetricsTests()
    {
        _meterFactory = new SimpleMeterFactory();
        _sloMetrics = new SloMetrics(_meterFactory);

        // Get the meter that was created by SloMetrics
        _meter = _meterFactory.GetMeter(SloMetrics.MeterName)!;

        // Create collectors for each metric
        _commandSuccessRate24hCollector = new MetricCollector<double>(
            _meter,
            "discordbot.slo.command.success_rate_24h");

        _apiSuccessRate24hCollector = new MetricCollector<double>(
            _meter,
            "discordbot.slo.api.success_rate_24h");

        _commandP99Latency1hCollector = new MetricCollector<double>(
            _meter,
            "discordbot.slo.command.p99_latency_1h");

        _errorBudgetRemainingCollector = new MetricCollector<double>(
            _meter,
            "discordbot.slo.error_budget.remaining");

        _uptimePercentage30dCollector = new MetricCollector<double>(
            _meter,
            "discordbot.slo.uptime.percentage_30d");
    }

    [Fact]
    public void UpdateCommandSuccessRate24h_UpdatesGaugeValue()
    {
        // Arrange
        const double expectedRate = 99.5;

        // Act
        _sloMetrics.UpdateCommandSuccessRate24h(expectedRate);

        // Assert
        _commandSuccessRate24hCollector.RecordObservableInstruments();
        var measurements = _commandSuccessRate24hCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a single command success rate measurement should be recorded");
        measurements.Single().Value.Should().Be(expectedRate,
            "the command success rate should match the updated value");
    }

    [Fact]
    public void UpdateCommandSuccessRate24h_MultipleUpdates_ReflectsLatestValue()
    {
        // Arrange & Act
        _sloMetrics.UpdateCommandSuccessRate24h(95.0);
        _sloMetrics.UpdateCommandSuccessRate24h(98.5);
        _sloMetrics.UpdateCommandSuccessRate24h(99.9);

        // Assert
        _commandSuccessRate24hCollector.RecordObservableInstruments();
        var measurements = _commandSuccessRate24hCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("only the latest value should be observable");
        measurements.Single().Value.Should().Be(99.9,
            "the command success rate should reflect the most recent update");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(50.0)]
    [InlineData(100.0)]
    public void UpdateCommandSuccessRate24h_WithVariousPercentages_AcceptsValue(double percentage)
    {
        // Act
        _sloMetrics.UpdateCommandSuccessRate24h(percentage);

        // Assert
        _commandSuccessRate24hCollector.RecordObservableInstruments();
        var measurements = _commandSuccessRate24hCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a measurement should be recorded");
        measurements.Single().Value.Should().Be(percentage,
            $"the command success rate should be {percentage}%");
    }

    [Fact]
    public void UpdateApiSuccessRate24h_UpdatesGaugeValue()
    {
        // Arrange
        const double expectedRate = 99.99;

        // Act
        _sloMetrics.UpdateApiSuccessRate24h(expectedRate);

        // Assert
        _apiSuccessRate24hCollector.RecordObservableInstruments();
        var measurements = _apiSuccessRate24hCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a single API success rate measurement should be recorded");
        measurements.Single().Value.Should().Be(expectedRate,
            "the API success rate should match the updated value");
    }

    [Fact]
    public void UpdateApiSuccessRate24h_MultipleUpdates_ReflectsLatestValue()
    {
        // Arrange & Act
        _sloMetrics.UpdateApiSuccessRate24h(97.0);
        _sloMetrics.UpdateApiSuccessRate24h(98.0);
        _sloMetrics.UpdateApiSuccessRate24h(99.5);

        // Assert
        _apiSuccessRate24hCollector.RecordObservableInstruments();
        var measurements = _apiSuccessRate24hCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("only the latest value should be observable");
        measurements.Single().Value.Should().Be(99.5,
            "the API success rate should reflect the most recent update");
    }

    [Fact]
    public void UpdateCommandP99Latency1h_UpdatesGaugeValue()
    {
        // Arrange
        const double expectedLatency = 125.75;

        // Act
        _sloMetrics.UpdateCommandP99Latency1h(expectedLatency);

        // Assert
        _commandP99Latency1hCollector.RecordObservableInstruments();
        var measurements = _commandP99Latency1hCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a single p99 latency measurement should be recorded");
        measurements.Single().Value.Should().Be(expectedLatency,
            "the p99 latency should match the updated value");
    }

    [Fact]
    public void UpdateCommandP99Latency1h_MultipleUpdates_ReflectsLatestValue()
    {
        // Arrange & Act
        _sloMetrics.UpdateCommandP99Latency1h(100.0);
        _sloMetrics.UpdateCommandP99Latency1h(150.0);
        _sloMetrics.UpdateCommandP99Latency1h(75.5);

        // Assert
        _commandP99Latency1hCollector.RecordObservableInstruments();
        var measurements = _commandP99Latency1hCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("only the latest value should be observable");
        measurements.Single().Value.Should().Be(75.5,
            "the p99 latency should reflect the most recent update");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(50.0)]
    [InlineData(250.5)]
    [InlineData(1000.0)]
    public void UpdateCommandP99Latency1h_WithVariousLatencies_AcceptsValue(double latencyMs)
    {
        // Act
        _sloMetrics.UpdateCommandP99Latency1h(latencyMs);

        // Assert
        _commandP99Latency1hCollector.RecordObservableInstruments();
        var measurements = _commandP99Latency1hCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a measurement should be recorded");
        measurements.Single().Value.Should().Be(latencyMs,
            $"the p99 latency should be {latencyMs}ms");
    }

    [Fact]
    public void UpdateErrorBudgetRemaining_UpdatesGaugeValue()
    {
        // Arrange
        const double expectedBudget = 85.5;

        // Act
        _sloMetrics.UpdateErrorBudgetRemaining(expectedBudget);

        // Assert
        _errorBudgetRemainingCollector.RecordObservableInstruments();
        var measurements = _errorBudgetRemainingCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a single error budget measurement should be recorded");
        measurements.Single().Value.Should().Be(expectedBudget,
            "the error budget remaining should match the updated value");
    }

    [Fact]
    public void UpdateErrorBudgetRemaining_MultipleUpdates_ReflectsLatestValue()
    {
        // Arrange & Act
        _sloMetrics.UpdateErrorBudgetRemaining(100.0);
        _sloMetrics.UpdateErrorBudgetRemaining(75.0);
        _sloMetrics.UpdateErrorBudgetRemaining(50.0);

        // Assert
        _errorBudgetRemainingCollector.RecordObservableInstruments();
        var measurements = _errorBudgetRemainingCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("only the latest value should be observable");
        measurements.Single().Value.Should().Be(50.0,
            "the error budget remaining should reflect the most recent update");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(25.5)]
    [InlineData(100.0)]
    public void UpdateErrorBudgetRemaining_WithVariousPercentages_AcceptsValue(double budgetPercentage)
    {
        // Act
        _sloMetrics.UpdateErrorBudgetRemaining(budgetPercentage);

        // Assert
        _errorBudgetRemainingCollector.RecordObservableInstruments();
        var measurements = _errorBudgetRemainingCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a measurement should be recorded");
        measurements.Single().Value.Should().Be(budgetPercentage,
            $"the error budget remaining should be {budgetPercentage}%");
    }

    [Fact]
    public void UpdateUptimePercentage30d_UpdatesGaugeValue()
    {
        // Arrange
        const double expectedUptime = 99.95;

        // Act
        _sloMetrics.UpdateUptimePercentage30d(expectedUptime);

        // Assert
        _uptimePercentage30dCollector.RecordObservableInstruments();
        var measurements = _uptimePercentage30dCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a single uptime percentage measurement should be recorded");
        measurements.Single().Value.Should().Be(expectedUptime,
            "the uptime percentage should match the updated value");
    }

    [Fact]
    public void UpdateUptimePercentage30d_MultipleUpdates_ReflectsLatestValue()
    {
        // Arrange & Act
        _sloMetrics.UpdateUptimePercentage30d(99.0);
        _sloMetrics.UpdateUptimePercentage30d(99.5);
        _sloMetrics.UpdateUptimePercentage30d(99.99);

        // Assert
        _uptimePercentage30dCollector.RecordObservableInstruments();
        var measurements = _uptimePercentage30dCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("only the latest value should be observable");
        measurements.Single().Value.Should().Be(99.99,
            "the uptime percentage should reflect the most recent update");
    }

    [Theory]
    [InlineData(95.0)]
    [InlineData(99.9)]
    [InlineData(100.0)]
    public void UpdateUptimePercentage30d_WithVariousPercentages_AcceptsValue(double uptimePercentage)
    {
        // Act
        _sloMetrics.UpdateUptimePercentage30d(uptimePercentage);

        // Assert
        _uptimePercentage30dCollector.RecordObservableInstruments();
        var measurements = _uptimePercentage30dCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a measurement should be recorded");
        measurements.Single().Value.Should().Be(uptimePercentage,
            $"the uptime percentage should be {uptimePercentage}%");
    }

    [Fact]
    public void MeterName_IsCorrect()
    {
        // Assert
        SloMetrics.MeterName.Should().Be("DiscordBot.SLO",
            "the meter name should match the expected value for OpenTelemetry collection");
    }

    [Fact]
    public void Dispose_DisposesMetricsCorrectly()
    {
        // Arrange
        var meterFactory = new SimpleMeterFactory();
        var metrics = new SloMetrics(meterFactory);

        // Act
        var act = () => metrics.Dispose();

        // Assert
        act.Should().NotThrow("Dispose should complete without errors");
    }

    public void Dispose()
    {
        _sloMetrics.Dispose();
        _meter.Dispose();
    }
}
