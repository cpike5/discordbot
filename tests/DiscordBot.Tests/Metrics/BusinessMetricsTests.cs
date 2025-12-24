using System.Diagnostics.Metrics;
using DiscordBot.Bot.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace DiscordBot.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="BusinessMetrics"/>.
/// Tests verify that business-level metrics are recorded correctly with appropriate tags and values.
/// </summary>
public class BusinessMetricsTests : IDisposable
{
    private readonly SimpleMeterFactory _meterFactory;
    private readonly Meter _meter;
    private readonly BusinessMetrics _businessMetrics;
    private readonly MetricCollector<long> _guildJoinCollector;
    private readonly MetricCollector<long> _guildLeaveCollector;
    private readonly MetricCollector<long> _featureUsageCollector;
    private readonly MetricCollector<long> _guildsJoinedTodayCollector;
    private readonly MetricCollector<long> _guildsLeftTodayCollector;
    private readonly MetricCollector<long> _activeGuildsDailyCollector;
    private readonly MetricCollector<long> _activeUsers7dCollector;
    private readonly MetricCollector<long> _commandsTodayCollector;

    public BusinessMetricsTests()
    {
        _meterFactory = new SimpleMeterFactory();
        _businessMetrics = new BusinessMetrics(_meterFactory);

        // Get the meter that was created by BusinessMetrics
        _meter = _meterFactory.GetMeter(BusinessMetrics.MeterName)!;

        // Create collectors for each metric
        _guildJoinCollector = new MetricCollector<long>(
            _meter,
            "discordbot.business.guild.join");

        _guildLeaveCollector = new MetricCollector<long>(
            _meter,
            "discordbot.business.guild.leave");

        _featureUsageCollector = new MetricCollector<long>(
            _meter,
            "discordbot.business.feature.usage");

        _guildsJoinedTodayCollector = new MetricCollector<long>(
            _meter,
            "discordbot.business.guilds.joined_today");

        _guildsLeftTodayCollector = new MetricCollector<long>(
            _meter,
            "discordbot.business.guilds.left_today");

        _activeGuildsDailyCollector = new MetricCollector<long>(
            _meter,
            "discordbot.business.guilds.active_daily");

        _activeUsers7dCollector = new MetricCollector<long>(
            _meter,
            "discordbot.business.users.active_7d");

        _commandsTodayCollector = new MetricCollector<long>(
            _meter,
            "discordbot.business.commands.today");
    }

    [Fact]
    public void RecordGuildJoin_IncrementsCounter()
    {
        // Act
        _businessMetrics.RecordGuildJoin();

        // Assert
        var measurements = _guildJoinCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single guild join event should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(1, "counter should increment by 1");
    }

    [Fact]
    public void RecordGuildJoin_MultipleJoins_RecordsEachSeparately()
    {
        // Act
        _businessMetrics.RecordGuildJoin();
        _businessMetrics.RecordGuildJoin();
        _businessMetrics.RecordGuildJoin();

        // Assert
        var measurements = _guildJoinCollector.GetMeasurementSnapshot();
        measurements.Should().HaveCount(3, "three separate guild join events should be recorded");
        measurements.Should().AllSatisfy(m => m.Value.Should().Be(1, "each event increments by 1"));
    }

    [Fact]
    public void RecordGuildLeave_IncrementsCounter()
    {
        // Act
        _businessMetrics.RecordGuildLeave();

        // Assert
        var measurements = _guildLeaveCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single guild leave event should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(1, "counter should increment by 1");
    }

    [Fact]
    public void RecordGuildLeave_MultipleLeaves_RecordsEachSeparately()
    {
        // Act
        _businessMetrics.RecordGuildLeave();
        _businessMetrics.RecordGuildLeave();

        // Assert
        var measurements = _guildLeaveCollector.GetMeasurementSnapshot();
        measurements.Should().HaveCount(2, "two separate guild leave events should be recorded");
        measurements.Should().AllSatisfy(m => m.Value.Should().Be(1, "each event increments by 1"));
    }

    [Fact]
    public void RecordFeatureUsage_IncrementsCounterWithFeatureTag()
    {
        // Arrange
        const string featureName = "interactive_buttons";

        // Act
        _businessMetrics.RecordFeatureUsage(featureName);

        // Assert
        var measurements = _featureUsageCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single feature usage event should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(1, "counter should increment by 1");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("feature", featureName),
            "the feature tag should be set");
    }

    [Theory]
    [InlineData("slash_commands")]
    [InlineData("message_components")]
    [InlineData("modals")]
    public void RecordFeatureUsage_WithDifferentFeatures_RecordsCorrectFeature(string featureName)
    {
        // Act
        _businessMetrics.RecordFeatureUsage(featureName);

        // Assert
        var measurements = _featureUsageCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single feature usage event should be recorded");

        var measurement = measurements.Single();
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("feature", featureName),
            $"the feature tag should be '{featureName}'");
    }

    [Fact]
    public void RecordFeatureUsage_MultipleFeatures_RecordsEachSeparately()
    {
        // Act
        _businessMetrics.RecordFeatureUsage("feature1");
        _businessMetrics.RecordFeatureUsage("feature2");
        _businessMetrics.RecordFeatureUsage("feature1");

        // Assert
        var measurements = _featureUsageCollector.GetMeasurementSnapshot();
        measurements.Should().HaveCount(3, "three separate feature usage events should be recorded");

        var feature1Count = measurements.Count(m =>
            m.Tags.Contains(new KeyValuePair<string, object?>("feature", "feature1")));
        feature1Count.Should().Be(2, "feature1 should be recorded twice");

        var feature2Count = measurements.Count(m =>
            m.Tags.Contains(new KeyValuePair<string, object?>("feature", "feature2")));
        feature2Count.Should().Be(1, "feature2 should be recorded once");
    }

    [Fact]
    public void UpdateGuildsJoinedToday_UpdatesGaugeValue()
    {
        // Arrange
        const long expectedCount = 15;

        // Act
        _businessMetrics.UpdateGuildsJoinedToday(expectedCount);

        // Assert - Trigger observable gauge collection
        _guildsJoinedTodayCollector.RecordObservableInstruments();
        var measurements = _guildsJoinedTodayCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a single guild joined today measurement should be recorded");
        measurements.Single().Value.Should().Be(expectedCount,
            "the guilds joined today count should match the updated value");
    }

    [Fact]
    public void UpdateGuildsJoinedToday_MultipleUpdates_ReflectsLatestValue()
    {
        // Arrange & Act
        _businessMetrics.UpdateGuildsJoinedToday(5);
        _businessMetrics.UpdateGuildsJoinedToday(10);
        _businessMetrics.UpdateGuildsJoinedToday(20);

        // Assert
        _guildsJoinedTodayCollector.RecordObservableInstruments();
        var measurements = _guildsJoinedTodayCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("only the latest value should be observable");
        measurements.Single().Value.Should().Be(20,
            "the guilds joined today count should reflect the most recent update");
    }

    [Fact]
    public void UpdateGuildsJoinedToday_WithZero_AcceptsZeroValue()
    {
        // Act
        _businessMetrics.UpdateGuildsJoinedToday(0);

        // Assert
        _guildsJoinedTodayCollector.RecordObservableInstruments();
        var measurements = _guildsJoinedTodayCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a measurement should be recorded even for zero");
        measurements.Single().Value.Should().Be(0,
            "zero is a valid count (no guilds joined today)");
    }

    [Fact]
    public void UpdateGuildsLeftToday_UpdatesGaugeValue()
    {
        // Arrange
        const long expectedCount = 3;

        // Act
        _businessMetrics.UpdateGuildsLeftToday(expectedCount);

        // Assert
        _guildsLeftTodayCollector.RecordObservableInstruments();
        var measurements = _guildsLeftTodayCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a single guild left today measurement should be recorded");
        measurements.Single().Value.Should().Be(expectedCount,
            "the guilds left today count should match the updated value");
    }

    [Fact]
    public void UpdateGuildsLeftToday_MultipleUpdates_ReflectsLatestValue()
    {
        // Arrange & Act
        _businessMetrics.UpdateGuildsLeftToday(2);
        _businessMetrics.UpdateGuildsLeftToday(5);
        _businessMetrics.UpdateGuildsLeftToday(1);

        // Assert
        _guildsLeftTodayCollector.RecordObservableInstruments();
        var measurements = _guildsLeftTodayCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("only the latest value should be observable");
        measurements.Single().Value.Should().Be(1,
            "the guilds left today count should reflect the most recent update");
    }

    [Fact]
    public void UpdateActiveGuildsDaily_UpdatesGaugeValue()
    {
        // Arrange
        const long expectedCount = 42;

        // Act
        _businessMetrics.UpdateActiveGuildsDaily(expectedCount);

        // Assert
        _activeGuildsDailyCollector.RecordObservableInstruments();
        var measurements = _activeGuildsDailyCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a single active guilds daily measurement should be recorded");
        measurements.Single().Value.Should().Be(expectedCount,
            "the active guilds daily count should match the updated value");
    }

    [Fact]
    public void UpdateActiveGuildsDaily_MultipleUpdates_ReflectsLatestValue()
    {
        // Arrange & Act
        _businessMetrics.UpdateActiveGuildsDaily(10);
        _businessMetrics.UpdateActiveGuildsDaily(25);
        _businessMetrics.UpdateActiveGuildsDaily(30);

        // Assert
        _activeGuildsDailyCollector.RecordObservableInstruments();
        var measurements = _activeGuildsDailyCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("only the latest value should be observable");
        measurements.Single().Value.Should().Be(30,
            "the active guilds daily count should reflect the most recent update");
    }

    [Fact]
    public void UpdateActiveUsers7d_UpdatesGaugeValue()
    {
        // Arrange
        const long expectedCount = 12345;

        // Act
        _businessMetrics.UpdateActiveUsers7d(expectedCount);

        // Assert
        _activeUsers7dCollector.RecordObservableInstruments();
        var measurements = _activeUsers7dCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a single active users 7d measurement should be recorded");
        measurements.Single().Value.Should().Be(expectedCount,
            "the active users 7d count should match the updated value");
    }

    [Fact]
    public void UpdateActiveUsers7d_MultipleUpdates_ReflectsLatestValue()
    {
        // Arrange & Act
        _businessMetrics.UpdateActiveUsers7d(100);
        _businessMetrics.UpdateActiveUsers7d(500);
        _businessMetrics.UpdateActiveUsers7d(1000);

        // Assert
        _activeUsers7dCollector.RecordObservableInstruments();
        var measurements = _activeUsers7dCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("only the latest value should be observable");
        measurements.Single().Value.Should().Be(1000,
            "the active users 7d count should reflect the most recent update");
    }

    [Fact]
    public void UpdateCommandsToday_UpdatesGaugeValue()
    {
        // Arrange
        const long expectedCount = 5678;

        // Act
        _businessMetrics.UpdateCommandsToday(expectedCount);

        // Assert
        _commandsTodayCollector.RecordObservableInstruments();
        var measurements = _commandsTodayCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a single commands today measurement should be recorded");
        measurements.Single().Value.Should().Be(expectedCount,
            "the commands today count should match the updated value");
    }

    [Fact]
    public void UpdateCommandsToday_MultipleUpdates_ReflectsLatestValue()
    {
        // Arrange & Act
        _businessMetrics.UpdateCommandsToday(100);
        _businessMetrics.UpdateCommandsToday(250);
        _businessMetrics.UpdateCommandsToday(500);

        // Assert
        _commandsTodayCollector.RecordObservableInstruments();
        var measurements = _commandsTodayCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("only the latest value should be observable");
        measurements.Single().Value.Should().Be(500,
            "the commands today count should reflect the most recent update");
    }

    [Fact]
    public void MeterName_IsCorrect()
    {
        // Assert
        BusinessMetrics.MeterName.Should().Be("DiscordBot.Business",
            "the meter name should match the expected value for OpenTelemetry collection");
    }

    [Fact]
    public void Dispose_DisposesMetricsCorrectly()
    {
        // Arrange
        var meterFactory = new SimpleMeterFactory();
        var metrics = new BusinessMetrics(meterFactory);

        // Act
        var act = () => metrics.Dispose();

        // Assert
        act.Should().NotThrow("Dispose should complete without errors");
    }

    public void Dispose()
    {
        _businessMetrics.Dispose();
        _meter.Dispose();
    }
}
