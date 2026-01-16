using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CpuHistoryService"/>.
/// Tests cover recording samples, retrieving current CPU, querying time-windowed samples,
/// calculating statistics with percentiles, circular buffer overflow behavior, and memory reporting.
/// </summary>
public class CpuHistoryServiceTests
{
    private readonly CpuHistoryService _service;
    private readonly PerformanceMetricsOptions _options;

    public CpuHistoryServiceTests()
    {
        _options = new PerformanceMetricsOptions
        {
            CpuRetentionHours = 24,
            CpuSampleIntervalSeconds = 5
        };

        _service = new CpuHistoryService(
            NullLogger<CpuHistoryService>.Instance,
            Options.Create(_options));
    }

    [Fact]
    public void RecordSample_StoresSampleWithTimestamp()
    {
        // Arrange
        const double cpuPercent = 45.5;
        var beforeTimestamp = DateTime.UtcNow;

        // Act
        _service.RecordSample(cpuPercent);

        // Assert
        var samples = _service.GetSamples(hours: 1);
        samples.Should().HaveCount(1, "one sample was recorded");

        var sample = samples[0];
        sample.CpuPercent.Should().Be(cpuPercent, "CPU value should match");
        sample.Timestamp.Should().BeOnOrAfter(beforeTimestamp, "timestamp should be recent");
        sample.Timestamp.Should().BeOnOrBefore(DateTime.UtcNow, "timestamp should not be in the future");
    }

    [Fact]
    public void GetCurrentCpu_ReturnsLastRecordedValue()
    {
        // Arrange
        _service.RecordSample(25.0);
        _service.RecordSample(50.0);
        _service.RecordSample(75.0);

        // Act
        var currentCpu = _service.GetCurrentCpu();

        // Assert
        currentCpu.Should().Be(75.0, "should return the most recently recorded CPU value");
    }

    [Fact]
    public void GetCurrentCpu_ReturnsZeroWhenNoSamples()
    {
        // Act
        var currentCpu = _service.GetCurrentCpu();

        // Assert
        currentCpu.Should().Be(0, "should return zero when no samples have been recorded");
    }

    [Fact]
    public void GetSamples_ReturnsAllSamplesWithinTimeRange()
    {
        // Arrange
        _service.RecordSample(30.0);
        _service.RecordSample(45.0);
        _service.RecordSample(60.0);

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
        _service.RecordSample(20.0);
        _service.RecordSample(50.0);
        _service.RecordSample(80.0);

        // Act
        var stats = _service.GetStatistics(hours: 24);

        // Assert
        stats.SampleCount.Should().Be(3, "three samples were recorded");
        stats.Average.Should().Be(50.0, "average of 20, 50, 80 is 50");
        stats.Min.Should().Be(20.0, "minimum value is 20");
        stats.Max.Should().Be(80.0, "maximum value is 80");
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
    public void GetStatistics_ReturnsZerosWhenNoSamples()
    {
        // Act
        var stats = _service.GetStatistics(hours: 24);

        // Assert
        stats.SampleCount.Should().Be(0, "no samples were recorded");
        stats.Average.Should().Be(0, "average should be zero");
        stats.Min.Should().Be(0, "min should be zero");
        stats.Max.Should().Be(0, "max should be zero");
        stats.P50.Should().Be(0, "P50 should be zero");
        stats.P95.Should().Be(0, "P95 should be zero");
        stats.P99.Should().Be(0, "P99 should be zero");
    }

    [Fact]
    public void CircularBuffer_OldestSamplesAreOverwritten()
    {
        // Arrange - Create a service with very small capacity (2 samples)
        var smallOptions = new PerformanceMetricsOptions
        {
            CpuRetentionHours = 1,
            CpuSampleIntervalSeconds = 1800 // 30 minutes interval = 2 samples for 1 hour
        };

        var smallService = new CpuHistoryService(
            NullLogger<CpuHistoryService>.Instance,
            Options.Create(smallOptions));

        // Act - Record more samples than capacity
        smallService.RecordSample(10.0);
        smallService.RecordSample(20.0);
        smallService.RecordSample(30.0); // This should overwrite the first sample (10.0)

        // Assert
        var samples = smallService.GetSamples(hours: 24);
        samples.Should().HaveCount(2, "circular buffer maintains max capacity");
        samples.Should().NotContain(s => s.CpuPercent == 10.0, "oldest sample should be overwritten");
        samples.Should().Contain(s => s.CpuPercent == 20.0, "second sample should remain");
        samples.Should().Contain(s => s.CpuPercent == 30.0, "newest sample should be present");
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
                    _service.RecordSample(threadId * 10.0 + (i % 10));
                }
            })
        ).ToArray();

        Task.WaitAll(tasks);

        // Assert
        var samples = _service.GetSamples(hours: 24);
        samples.Should().HaveCount(threadCount * samplesPerThread, "all samples from all threads should be recorded");

        var currentCpu = _service.GetCurrentCpu();
        currentCpu.Should().BeInRange(0, 100, "current CPU should be valid");

        var stats = _service.GetStatistics(hours: 24);
        stats.SampleCount.Should().Be(threadCount * samplesPerThread, "statistics should reflect all samples");
    }

    [Fact]
    public void GetMemoryReport_ReturnsCorrectValues()
    {
        // Arrange
        _service.RecordSample(50.0);
        _service.RecordSample(60.0);
        _service.RecordSample(70.0);

        // Act
        var report = _service.GetMemoryReport();

        // Assert
        report.ServiceName.Should().Be("CPU History", "service name should be correct");
        report.ItemCount.Should().Be(3, "item count should match sample count");

        // Max samples = (24h * 3600s) / 5s = 17,280 samples
        // Sample size = 16 bytes (DateTime 8 + double 8)
        var expectedMaxSamples = (_options.CpuRetentionHours * 3600) / _options.CpuSampleIntervalSeconds;
        var expectedBytes = expectedMaxSamples * 16;
        report.EstimatedBytes.Should().Be(expectedBytes, "estimated bytes should be calculated correctly");

        report.Details.Should().Contain("Circular buffer", "details should describe buffer");
        report.Details.Should().Contain("3/", "details should show current sample count");
        report.Details.Should().Contain("24h retention", "details should show retention period");
    }
}
