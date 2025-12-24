using System.Diagnostics.Metrics;
using DiscordBot.Bot.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace DiscordBot.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="BotMetrics"/>.
/// Tests verify that OpenTelemetry metrics are recorded correctly with appropriate tags and values.
/// </summary>
public class BotMetricsTests : IDisposable
{
    private readonly SimpleMeterFactory _meterFactory;
    private readonly Meter _meter;
    private readonly BotMetrics _botMetrics;
    private readonly MetricCollector<long> _commandCounterCollector;
    private readonly MetricCollector<double> _commandDurationCollector;
    private readonly MetricCollector<long> _activeCommandsCollector;
    private readonly MetricCollector<long> _rateLimitViolationsCollector;
    private readonly MetricCollector<long> _componentCounterCollector;
    private readonly MetricCollector<double> _componentDurationCollector;
    private readonly MetricCollector<long> _activeGuildsCollector;
    private readonly MetricCollector<long> _uniqueUsersCollector;

    public BotMetricsTests()
    {
        _meterFactory = new SimpleMeterFactory();
        _botMetrics = new BotMetrics(_meterFactory);

        // Get the meter that was created by BotMetrics
        _meter = _meterFactory.GetMeter(BotMetrics.MeterName)!;

        // Create collectors for each metric
        _commandCounterCollector = new MetricCollector<long>(
            _meter,
            "discordbot.command.count");

        _commandDurationCollector = new MetricCollector<double>(
            _meter,
            "discordbot.command.duration");

        _activeCommandsCollector = new MetricCollector<long>(
            _meter,
            "discordbot.command.active");

        _rateLimitViolationsCollector = new MetricCollector<long>(
            _meter,
            "discordbot.ratelimit.violations");

        _componentCounterCollector = new MetricCollector<long>(
            _meter,
            "discordbot.component.count");

        _componentDurationCollector = new MetricCollector<double>(
            _meter,
            "discordbot.component.duration");

        _activeGuildsCollector = new MetricCollector<long>(
            _meter,
            "discordbot.guilds.active");

        _uniqueUsersCollector = new MetricCollector<long>(
            _meter,
            "discordbot.users.unique");
    }

    [Fact]
    public void RecordCommandExecution_SuccessfulCommand_IncrementsCounterWithCorrectTags()
    {
        // Arrange
        const string commandName = "ping";
        const bool success = true;
        const double durationMs = 123.45;

        // Act
        _botMetrics.RecordCommandExecution(commandName, success, durationMs);

        // Assert
        var measurements = _commandCounterCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single command execution should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(1, "counter should increment by 1");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("command", commandName),
            "the command tag should be set");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("status", "success"),
            "the status tag should be 'success' for successful commands");
    }

    [Fact]
    public void RecordCommandExecution_FailedCommand_IncrementsCounterWithFailureStatus()
    {
        // Arrange
        const string commandName = "error-command";
        const bool success = false;
        const double durationMs = 50.0;

        // Act
        _botMetrics.RecordCommandExecution(commandName, success, durationMs);

        // Assert
        var measurements = _commandCounterCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single command execution should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(1, "counter should increment by 1");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("command", commandName),
            "the command tag should be set");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("status", "failure"),
            "the status tag should be 'failure' for failed commands");
    }

    [Fact]
    public void RecordCommandExecution_RecordsDurationHistogramWithCorrectValue()
    {
        // Arrange
        const string commandName = "slowcommand";
        const bool success = true;
        const double durationMs = 567.89;

        // Act
        _botMetrics.RecordCommandExecution(commandName, success, durationMs);

        // Assert
        var measurements = _commandDurationCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single duration measurement should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(durationMs, "the recorded duration should match the provided value");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("command", commandName),
            "the command tag should be set on duration histogram");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("status", "success"),
            "the status tag should be set on duration histogram");
    }

    [Fact]
    public void RecordCommandExecution_WithGuildId_DoesNotIncludeGuildIdTag()
    {
        // Arrange
        const string commandName = "guild-command";
        const bool success = true;
        const double durationMs = 100.0;
        const ulong guildId = 123456789012345678;

        // Act
        _botMetrics.RecordCommandExecution(commandName, success, durationMs, guildId);

        // Assert
        var measurements = _commandCounterCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single command execution should be recorded");

        var measurement = measurements.Single();
        measurement.Tags.Should().NotContainKey("guild_id",
            "guild_id should not be included to avoid cardinality explosion");
    }

    [Fact]
    public void RecordCommandExecution_MultipleCommands_RecordsEachSeparately()
    {
        // Arrange
        const string command1 = "ping";
        const string command2 = "help";

        // Act
        _botMetrics.RecordCommandExecution(command1, true, 100.0);
        _botMetrics.RecordCommandExecution(command2, true, 200.0);

        // Assert
        var counterMeasurements = _commandCounterCollector.GetMeasurementSnapshot();
        counterMeasurements.Should().HaveCount(2, "two separate command executions should be recorded");

        var durationMeasurements = _commandDurationCollector.GetMeasurementSnapshot();
        durationMeasurements.Should().HaveCount(2, "two separate duration measurements should be recorded");
    }

    [Fact]
    public void IncrementActiveCommands_IncrementsCounterWithCommandTag()
    {
        // Arrange
        const string commandName = "active-command";

        // Act
        _botMetrics.IncrementActiveCommands(commandName);

        // Assert
        var measurements = _activeCommandsCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single increment should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(1, "active commands should increment by 1");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("command", commandName),
            "the command tag should be set");
    }

    [Fact]
    public void DecrementActiveCommands_DecrementsCounterWithCommandTag()
    {
        // Arrange
        const string commandName = "finishing-command";

        // Act
        _botMetrics.DecrementActiveCommands(commandName);

        // Assert
        var measurements = _activeCommandsCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single decrement should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(-1, "active commands should decrement by 1");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("command", commandName),
            "the command tag should be set");
    }

    [Fact]
    public void ActiveCommands_IncrementThenDecrement_RecordsBothOperations()
    {
        // Arrange
        const string commandName = "lifecycle-command";

        // Act
        _botMetrics.IncrementActiveCommands(commandName);
        _botMetrics.DecrementActiveCommands(commandName);

        // Assert
        var measurements = _activeCommandsCollector.GetMeasurementSnapshot();
        measurements.Should().HaveCount(2, "both increment and decrement should be recorded");

        var incrementMeasurement = measurements.First();
        incrementMeasurement.Value.Should().Be(1, "first operation should be an increment");

        var decrementMeasurement = measurements.Last();
        decrementMeasurement.Value.Should().Be(-1, "second operation should be a decrement");
    }

    [Fact]
    public void RecordRateLimitViolation_RecordsWithCorrectTags()
    {
        // Arrange
        const string commandName = "spam-command";
        const string target = "user";

        // Act
        _botMetrics.RecordRateLimitViolation(commandName, target);

        // Assert
        var measurements = _rateLimitViolationsCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single rate limit violation should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(1, "violation counter should increment by 1");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("command", commandName),
            "the command tag should be set");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("target", target),
            "the target tag should be set");
    }

    [Theory]
    [InlineData("user")]
    [InlineData("guild")]
    [InlineData("global")]
    public void RecordRateLimitViolation_WithDifferentTargets_RecordsCorrectTarget(string target)
    {
        // Arrange
        const string commandName = "rate-limited-command";

        // Act
        _botMetrics.RecordRateLimitViolation(commandName, target);

        // Assert
        var measurements = _rateLimitViolationsCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single rate limit violation should be recorded");

        var measurement = measurements.Single();
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("target", target),
            $"the target tag should be '{target}'");
    }

    [Fact]
    public void RecordComponentInteraction_SuccessfulButton_RecordsCorrectly()
    {
        // Arrange
        const string componentType = "button";
        const bool success = true;
        const double durationMs = 75.5;

        // Act
        _botMetrics.RecordComponentInteraction(componentType, success, durationMs);

        // Assert
        var counterMeasurements = _componentCounterCollector.GetMeasurementSnapshot();
        counterMeasurements.Should().ContainSingle("a single component interaction should be recorded");

        var counterMeasurement = counterMeasurements.Single();
        counterMeasurement.Value.Should().Be(1, "component counter should increment by 1");
        counterMeasurement.Tags.Should().Contain(new KeyValuePair<string, object?>("component_type", componentType),
            "the component_type tag should be set");
        counterMeasurement.Tags.Should().Contain(new KeyValuePair<string, object?>("status", "success"),
            "the status tag should be 'success' for successful interactions");
    }

    [Fact]
    public void RecordComponentInteraction_FailedSelectMenu_RecordsWithFailureStatus()
    {
        // Arrange
        const string componentType = "select_menu";
        const bool success = false;
        const double durationMs = 25.0;

        // Act
        _botMetrics.RecordComponentInteraction(componentType, success, durationMs);

        // Assert
        var measurements = _componentCounterCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single component interaction should be recorded");

        var measurement = measurements.Single();
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("component_type", componentType),
            "the component_type tag should be set");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("status", "failure"),
            "the status tag should be 'failure' for failed interactions");
    }

    [Fact]
    public void RecordComponentInteraction_RecordsDurationHistogram()
    {
        // Arrange
        const string componentType = "modal";
        const bool success = true;
        const double durationMs = 150.25;

        // Act
        _botMetrics.RecordComponentInteraction(componentType, success, durationMs);

        // Assert
        var measurements = _componentDurationCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single duration measurement should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(durationMs, "the recorded duration should match the provided value");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("component_type", componentType),
            "the component_type tag should be set on duration histogram");
    }

    [Theory]
    [InlineData("button")]
    [InlineData("select_menu")]
    [InlineData("modal")]
    public void RecordComponentInteraction_WithDifferentComponentTypes_RecordsCorrectType(string componentType)
    {
        // Arrange
        const bool success = true;
        const double durationMs = 100.0;

        // Act
        _botMetrics.RecordComponentInteraction(componentType, success, durationMs);

        // Assert
        var measurements = _componentCounterCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single component interaction should be recorded");

        var measurement = measurements.Single();
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("component_type", componentType),
            $"the component_type tag should be '{componentType}'");
    }

    [Fact]
    public void UpdateActiveGuildCount_UpdatesGaugeValue()
    {
        // Arrange
        const long expectedCount = 42;

        // Act
        _botMetrics.UpdateActiveGuildCount(expectedCount);

        // Assert - Trigger observable gauge collection
        _activeGuildsCollector.RecordObservableInstruments();
        var measurements = _activeGuildsCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a single guild count measurement should be recorded");
        measurements.Single().Value.Should().Be(expectedCount,
            "the active guild count should match the updated value");
    }

    [Fact]
    public void UpdateActiveGuildCount_MultipleUpdates_ReflectsLatestValue()
    {
        // Arrange & Act
        _botMetrics.UpdateActiveGuildCount(10);
        _botMetrics.UpdateActiveGuildCount(20);
        _botMetrics.UpdateActiveGuildCount(30);

        // Assert
        _activeGuildsCollector.RecordObservableInstruments();
        var measurements = _activeGuildsCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("only the latest value should be observable");
        measurements.Single().Value.Should().Be(30,
            "the active guild count should reflect the most recent update");
    }

    [Fact]
    public void UpdateUniqueUserCount_UpdatesGaugeValue()
    {
        // Arrange
        const long expectedCount = 12345;

        // Act
        _botMetrics.UpdateUniqueUserCount(expectedCount);

        // Assert
        _uniqueUsersCollector.RecordObservableInstruments();
        var measurements = _uniqueUsersCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a single user count measurement should be recorded");
        measurements.Single().Value.Should().Be(expectedCount,
            "the unique user count should match the updated value");
    }

    [Fact]
    public void UpdateUniqueUserCount_MultipleUpdates_ReflectsLatestValue()
    {
        // Arrange & Act
        _botMetrics.UpdateUniqueUserCount(100);
        _botMetrics.UpdateUniqueUserCount(500);
        _botMetrics.UpdateUniqueUserCount(1000);

        // Assert
        _uniqueUsersCollector.RecordObservableInstruments();
        var measurements = _uniqueUsersCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("only the latest value should be observable");
        measurements.Single().Value.Should().Be(1000,
            "the unique user count should reflect the most recent update");
    }

    [Fact]
    public void UpdateActiveGuildCount_WithZero_AcceptsZeroValue()
    {
        // Arrange
        const long zeroCount = 0;

        // Act
        _botMetrics.UpdateActiveGuildCount(zeroCount);

        // Assert
        _activeGuildsCollector.RecordObservableInstruments();
        var measurements = _activeGuildsCollector.GetMeasurementSnapshot();

        measurements.Should().ContainSingle("a measurement should be recorded even for zero");
        measurements.Single().Value.Should().Be(0,
            "zero is a valid guild count (e.g., bot not yet connected)");
    }

    [Fact]
    public void MeterName_IsCorrect()
    {
        // Assert
        BotMetrics.MeterName.Should().Be("DiscordBot.Bot",
            "the meter name should match the expected value for OpenTelemetry collection");
    }

    [Fact]
    public void Dispose_DisposesMetricsCorrectly()
    {
        // Arrange
        var meterFactory = new SimpleMeterFactory();
        var metrics = new BotMetrics(meterFactory);

        // Act
        var act = () => metrics.Dispose();

        // Assert
        act.Should().NotThrow("Dispose should complete without errors");
    }

    public void Dispose()
    {
        _botMetrics.Dispose();
        _meter.Dispose();
    }
}

/// <summary>
/// Simple meter factory for testing purposes.
/// Creates meters that can be used with MetricCollector.
/// </summary>
internal class SimpleMeterFactory : IMeterFactory
{
    private readonly Dictionary<string, Meter> _meters = new();

    public Meter Create(MeterOptions options)
    {
        if (_meters.TryGetValue(options.Name, out var existingMeter))
        {
            return existingMeter;
        }

        var meter = new Meter(options);
        _meters[options.Name] = meter;
        return meter;
    }

    public Meter? GetMeter(string name)
    {
        _meters.TryGetValue(name, out var meter);
        return meter;
    }

    public void Dispose()
    {
        foreach (var meter in _meters.Values)
        {
            meter.Dispose();
        }
        _meters.Clear();
    }
}
