using System.Diagnostics.Metrics;
using DiscordBot.Bot.Metrics;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace DiscordBot.Tests.Metrics;

/// <summary>
/// Unit tests for <see cref="ApiMetrics"/>.
/// Tests verify that API request metrics are recorded correctly with appropriate tags and values.
/// </summary>
public class ApiMetricsTests : IDisposable
{
    private readonly SimpleMeterFactory _meterFactory;
    private readonly Meter _meter;
    private readonly ApiMetrics _apiMetrics;
    private readonly MetricCollector<long> _requestCounterCollector;
    private readonly MetricCollector<double> _requestDurationCollector;
    private readonly MetricCollector<long> _activeRequestsCollector;

    public ApiMetricsTests()
    {
        _meterFactory = new SimpleMeterFactory();
        _apiMetrics = new ApiMetrics(_meterFactory);

        // Get the meter that was created by ApiMetrics
        _meter = _meterFactory.GetMeter(ApiMetrics.MeterName)!;

        // Create collectors for each metric
        _requestCounterCollector = new MetricCollector<long>(
            _meter,
            "discordbot.api.request.count");

        _requestDurationCollector = new MetricCollector<double>(
            _meter,
            "discordbot.api.request.duration");

        _activeRequestsCollector = new MetricCollector<long>(
            _meter,
            "discordbot.api.request.active");
    }

    [Fact]
    public void RecordRequest_IncrementsCounterWithCorrectTags()
    {
        // Arrange
        const string endpoint = "/api/guilds/{id}";
        const string method = "GET";
        const int statusCode = 200;
        const double durationMs = 45.67;

        // Act
        _apiMetrics.RecordRequest(endpoint, method, statusCode, durationMs);

        // Assert
        var measurements = _requestCounterCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single request should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(1, "counter should increment by 1");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("endpoint", endpoint),
            "the endpoint tag should be set");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("method", method),
            "the method tag should be set");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("status_code", statusCode.ToString()),
            "the status_code tag should be set as a string");
    }

    [Fact]
    public void RecordRequest_RecordsDurationHistogramWithCorrectValue()
    {
        // Arrange
        const string endpoint = "/api/commands";
        const string method = "POST";
        const int statusCode = 201;
        const double durationMs = 123.45;

        // Act
        _apiMetrics.RecordRequest(endpoint, method, statusCode, durationMs);

        // Assert
        var measurements = _requestDurationCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single duration measurement should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(durationMs, "the recorded duration should match the provided value");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("endpoint", endpoint),
            "the endpoint tag should be set on duration histogram");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("method", method),
            "the method tag should be set on duration histogram");
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("status_code", statusCode.ToString()),
            "the status_code tag should be set on duration histogram");
    }

    [Theory]
    [InlineData("GET", 200)]
    [InlineData("POST", 201)]
    [InlineData("PUT", 200)]
    [InlineData("DELETE", 204)]
    [InlineData("PATCH", 200)]
    public void RecordRequest_WithDifferentMethods_RecordsCorrectMethod(string method, int statusCode)
    {
        // Arrange
        const string endpoint = "/api/test";
        const double durationMs = 100.0;

        // Act
        _apiMetrics.RecordRequest(endpoint, method, statusCode, durationMs);

        // Assert
        var measurements = _requestCounterCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single request should be recorded");

        var measurement = measurements.Single();
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("method", method),
            $"the method tag should be '{method}'");
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    [InlineData(204)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    [InlineData(503)]
    public void RecordRequest_WithDifferentStatusCodes_RecordsCorrectStatusCode(int statusCode)
    {
        // Arrange
        const string endpoint = "/api/test";
        const string method = "GET";
        const double durationMs = 50.0;

        // Act
        _apiMetrics.RecordRequest(endpoint, method, statusCode, durationMs);

        // Assert
        var measurements = _requestCounterCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single request should be recorded");

        var measurement = measurements.Single();
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("status_code", statusCode.ToString()),
            $"the status_code tag should be '{statusCode}'");
    }

    [Fact]
    public void RecordRequest_MultipleRequests_RecordsEachSeparately()
    {
        // Arrange
        const string endpoint1 = "/api/guilds";
        const string endpoint2 = "/api/users";

        // Act
        _apiMetrics.RecordRequest(endpoint1, "GET", 200, 100.0);
        _apiMetrics.RecordRequest(endpoint2, "POST", 201, 150.0);

        // Assert
        var counterMeasurements = _requestCounterCollector.GetMeasurementSnapshot();
        counterMeasurements.Should().HaveCount(2, "two separate requests should be recorded");

        var durationMeasurements = _requestDurationCollector.GetMeasurementSnapshot();
        durationMeasurements.Should().HaveCount(2, "two separate duration measurements should be recorded");
    }

    [Fact]
    public void RecordRequest_WithNormalizedEndpoint_UsesPlaceholder()
    {
        // Arrange
        const string normalizedEndpoint = "/api/guilds/{id}";
        const string method = "GET";
        const int statusCode = 200;
        const double durationMs = 50.0;

        // Act
        _apiMetrics.RecordRequest(normalizedEndpoint, method, statusCode, durationMs);

        // Assert
        var measurements = _requestCounterCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single request should be recorded");

        var measurement = measurements.Single();
        measurement.Tags.Should().Contain(new KeyValuePair<string, object?>("endpoint", normalizedEndpoint),
            "normalized endpoints should use placeholders to prevent cardinality explosion");
    }

    [Fact]
    public void IncrementActiveRequests_IncrementsCounter()
    {
        // Act
        _apiMetrics.IncrementActiveRequests();

        // Assert
        var measurements = _activeRequestsCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single increment should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(1, "active requests should increment by 1");
        measurement.Tags.Should().BeEmpty("IncrementActiveRequests does not use tags");
    }

    [Fact]
    public void DecrementActiveRequests_DecrementsCounter()
    {
        // Act
        _apiMetrics.DecrementActiveRequests();

        // Assert
        var measurements = _activeRequestsCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single decrement should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(-1, "active requests should decrement by 1");
        measurement.Tags.Should().BeEmpty("DecrementActiveRequests does not use tags");
    }

    [Fact]
    public void ActiveRequests_IncrementThenDecrement_RecordsBothOperations()
    {
        // Act
        _apiMetrics.IncrementActiveRequests();
        _apiMetrics.DecrementActiveRequests();

        // Assert
        var measurements = _activeRequestsCollector.GetMeasurementSnapshot();
        measurements.Should().HaveCount(2, "both increment and decrement should be recorded");

        var incrementMeasurement = measurements.First();
        incrementMeasurement.Value.Should().Be(1, "first operation should be an increment");

        var decrementMeasurement = measurements.Last();
        decrementMeasurement.Value.Should().Be(-1, "second operation should be a decrement");
    }

    [Fact]
    public void ActiveRequests_MultipleIncrements_RecordsAllIncrements()
    {
        // Act
        _apiMetrics.IncrementActiveRequests();
        _apiMetrics.IncrementActiveRequests();
        _apiMetrics.IncrementActiveRequests();

        // Assert
        var measurements = _activeRequestsCollector.GetMeasurementSnapshot();
        measurements.Should().HaveCount(3, "three increments should be recorded");
        measurements.Should().OnlyContain(m => m.Value == 1,
            "all measurements should be increments of 1");
    }

    [Fact]
    public void ActiveRequests_MultipleDecrements_RecordsAllDecrements()
    {
        // Act
        _apiMetrics.DecrementActiveRequests();
        _apiMetrics.DecrementActiveRequests();
        _apiMetrics.DecrementActiveRequests();

        // Assert
        var measurements = _activeRequestsCollector.GetMeasurementSnapshot();
        measurements.Should().HaveCount(3, "three decrements should be recorded");
        measurements.Should().OnlyContain(m => m.Value == -1,
            "all measurements should be decrements of -1");
    }

    [Fact]
    public void RecordRequest_WithLongDuration_RecordsAccurately()
    {
        // Arrange
        const string endpoint = "/api/slow-operation";
        const string method = "GET";
        const int statusCode = 200;
        const double durationMs = 5432.10; // Long-running request

        // Act
        _apiMetrics.RecordRequest(endpoint, method, statusCode, durationMs);

        // Assert
        var measurements = _requestDurationCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single duration measurement should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(durationMs,
            "long durations should be recorded accurately without truncation");
    }

    [Fact]
    public void RecordRequest_WithZeroDuration_RecordsZero()
    {
        // Arrange
        const string endpoint = "/api/instant";
        const string method = "GET";
        const int statusCode = 200;
        const double durationMs = 0.0;

        // Act
        _apiMetrics.RecordRequest(endpoint, method, statusCode, durationMs);

        // Assert
        var measurements = _requestDurationCollector.GetMeasurementSnapshot();
        measurements.Should().ContainSingle("a single duration measurement should be recorded");

        var measurement = measurements.Single();
        measurement.Value.Should().Be(0.0,
            "zero duration is a valid measurement (though unlikely in practice)");
    }

    [Fact]
    public void MeterName_IsCorrect()
    {
        // Assert
        ApiMetrics.MeterName.Should().Be("DiscordBot.Api",
            "the meter name should match the expected value for OpenTelemetry collection");
    }

    [Fact]
    public void Dispose_DisposesMetricsCorrectly()
    {
        // Arrange
        var meterFactory = new SimpleMeterFactory();
        var metrics = new ApiMetrics(meterFactory);

        // Act
        var act = () => metrics.Dispose();

        // Assert
        act.Should().NotThrow("Dispose should complete without errors");
    }

    public void Dispose()
    {
        _apiMetrics.Dispose();
        _meter.Dispose();
    }
}
