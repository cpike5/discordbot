using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="LatencyHistoryService"/>.
/// Tests cover recording samples, retrieving current latency, querying time-windowed samples,
/// calculating statistics with percentiles, and circular buffer overflow behavior.
/// </summary>
public class LatencyHistoryServiceTests
{
    private readonly LatencyHistoryService _service;
    private readonly PerformanceMetricsOptions _options;

    public LatencyHistoryServiceTests()
    {
        _options = new PerformanceMetricsOptions
        {
            LatencyRetentionHours = 24,
            LatencySampleIntervalSeconds = 30
        };

        _service = new LatencyHistoryService(
            NullLogger<LatencyHistoryService>.Instance,
            Options.Create(_options));
    }

    [Fact]
    public void RecordSample_StoresSampleWithTimestamp()
    {
        // Arrange
        const int latencyMs = 50;
        var beforeTimestamp = DateTime.UtcNow;

        // Act
        _service.RecordSample(latencyMs);

        // Assert
        var samples = _service.GetSamples(hours: 1);
        samples.Should().HaveCount(1, "one sample was recorded");

        var sample = samples[0];
        sample.LatencyMs.Should().Be(latencyMs, "latency value should match");
        sample.Timestamp.Should().BeOnOrAfter(beforeTimestamp, "timestamp should be recent");
        sample.Timestamp.Should().BeOnOrBefore(DateTime.UtcNow, "timestamp should not be in the future");
    }

    [Fact]
    public void GetCurrentLatency_ReturnsLastRecordedValue()
    {
        // Arrange
        _service.RecordSample(50);
        _service.RecordSample(75);
        _service.RecordSample(100);

        // Act
        var currentLatency = _service.GetCurrentLatency();

        // Assert
        currentLatency.Should().Be(100, "should return the most recently recorded latency");
    }

    [Fact]
    public void GetCurrentLatency_ReturnsZeroWhenNoSamples()
    {
        // Act
        var currentLatency = _service.GetCurrentLatency();

        // Assert
        currentLatency.Should().Be(0, "should return zero when no samples have been recorded");
    }

    [Fact]
    public void GetSamples_ReturnsAllSamplesWithinTimeRange()
    {
        // Arrange
        _service.RecordSample(50);
        _service.RecordSample(60);
        _service.RecordSample(70);

        // Act
        var samples = _service.GetSamples(hours: 24);

        // Assert
        samples.Should().HaveCount(3, "all recent samples should be returned");
        samples.Should().BeInAscendingOrder(s => s.Timestamp, "samples should be in chronological order");
    }

    [Fact]
    public void GetSamples_ReturnsEmptyWhenNoSamplesInRange()
    {
        // Arrange - service has no samples

        // Act
        var samples = _service.GetSamples(hours: 1);

        // Assert
        samples.Should().BeEmpty("no samples exist");
    }

    [Fact]
    public void GetStatistics_CalculatesAverageMinMaxCorrectly()
    {
        // Arrange
        _service.RecordSample(50);
        _service.RecordSample(100);
        _service.RecordSample(150);

        // Act
        var stats = _service.GetStatistics(hours: 24);

        // Assert
        stats.SampleCount.Should().Be(3, "three samples were recorded");
        stats.Average.Should().Be(100.0, "average of 50, 100, 150 is 100");
        stats.Min.Should().Be(50, "minimum value is 50");
        stats.Max.Should().Be(150, "maximum value is 150");
    }

    [Fact]
    public void GetStatistics_CalculatesPercentilesCorrectly()
    {
        // Arrange - Record values from 1 to 100
        for (int i = 1; i <= 100; i++)
        {
            _service.RecordSample(i);
        }

        // Act
        var stats = _service.GetStatistics(hours: 24);

        // Assert
        stats.SampleCount.Should().Be(100, "100 samples were recorded");
        stats.P50.Should().BeInRange(49, 51, "P50 should be near the median");
        stats.P95.Should().BeInRange(94, 96, "P95 should be near the 95th percentile");
        stats.P99.Should().BeInRange(98, 100, "P99 should be near the 99th percentile");
    }

    [Fact]
    public void CircularBuffer_OldestSamplesAreOverwritten()
    {
        // Arrange - Create a service with very small capacity (2 samples)
        var smallOptions = new PerformanceMetricsOptions
        {
            LatencyRetentionHours = 1,
            LatencySampleIntervalSeconds = 1800 // 30 minutes interval = 2 samples for 1 hour
        };

        var smallService = new LatencyHistoryService(
            NullLogger<LatencyHistoryService>.Instance,
            Options.Create(smallOptions));

        // Act - Record more samples than capacity
        smallService.RecordSample(10);
        smallService.RecordSample(20);
        smallService.RecordSample(30); // This should overwrite the first sample (10)

        // Assert
        var samples = smallService.GetSamples(hours: 24);
        samples.Should().HaveCount(2, "circular buffer maintains max capacity");
        samples.Should().NotContain(s => s.LatencyMs == 10, "oldest sample should be overwritten");
        samples.Should().Contain(s => s.LatencyMs == 20, "second sample should remain");
        samples.Should().Contain(s => s.LatencyMs == 30, "newest sample should be present");
    }

    [Fact]
    public void ThreadSafety_ConcurrentRecordingsDoNotCorrupt()
    {
        // Arrange
        const int threadCount = 10;
        const int samplesPerThread = 100;

        // Act - Record samples concurrently from multiple threads
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(() =>
            {
                for (int i = 0; i < samplesPerThread; i++)
                {
                    _service.RecordSample(threadId * 1000 + i);
                }
            })
        ).ToArray();

        Task.WaitAll(tasks);

        // Assert
        var samples = _service.GetSamples(hours: 24);
        samples.Should().HaveCount(threadCount * samplesPerThread, "all samples from all threads should be recorded");

        var currentLatency = _service.GetCurrentLatency();
        currentLatency.Should().BeGreaterThanOrEqualTo(0, "current latency should be valid");

        var stats = _service.GetStatistics(hours: 24);
        stats.SampleCount.Should().Be(threadCount * samplesPerThread, "statistics should reflect all samples");
    }
}
