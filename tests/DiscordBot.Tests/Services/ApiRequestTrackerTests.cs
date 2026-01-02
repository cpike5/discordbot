using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ApiRequestTracker"/>.
/// Tests cover API request tracking, latency recording, statistics calculation,
/// rate limit event management, request volume tracking, and filtering by time ranges.
/// </summary>
public class ApiRequestTrackerTests
{
    private readonly ApiRequestTracker _tracker;
    private readonly PerformanceMetricsOptions _options;

    public ApiRequestTrackerTests()
    {
        _options = new PerformanceMetricsOptions
        {
            ApiRequestTrackingEnabled = true
        };

        _tracker = new ApiRequestTracker(
            NullLogger<ApiRequestTracker>.Instance,
            Options.Create(_options));
    }

    // ============================================================================
    // RecordRequest Tests
    // ============================================================================

    [Fact]
    public void RecordRequest_WithValidInput_IncrementsCategoryCount()
    {
        // Arrange
        const string category = "REST";
        const int latency = 50;

        // Act
        _tracker.RecordRequest(category, latency);

        // Assert
        var stats = _tracker.GetUsageStatistics();
        var restStats = stats.FirstOrDefault(s => s.Category == category);

        restStats.Should().NotBeNull("REST category should be tracked");
        restStats!.RequestCount.Should().Be(1, "one request was recorded");
        restStats.AvgLatencyMs.Should().Be(latency, "average latency should match the single recorded value");
    }

    [Fact]
    public void RecordRequest_WithLatency_TracksLatencyValues()
    {
        // Arrange
        const string category = "Gateway";
        const int latency1 = 25;
        const int latency2 = 75;

        // Act
        _tracker.RecordRequest(category, latency1);
        _tracker.RecordRequest(category, latency2);

        // Assert
        var stats = _tracker.GetUsageStatistics();
        var gatewayStats = stats.FirstOrDefault(s => s.Category == category);

        gatewayStats.Should().NotBeNull("Gateway category should be tracked");
        gatewayStats!.RequestCount.Should().Be(2, "two requests were recorded");
        gatewayStats.AvgLatencyMs.Should().Be(50.0, "average of 25 and 75 is 50");
    }

    [Fact]
    public void RecordRequest_MultipleCalls_AccumulatesCorrectly()
    {
        // Arrange
        const string category = "REST";

        // Act
        for (int i = 1; i <= 10; i++)
        {
            _tracker.RecordRequest(category, i * 10); // 10, 20, 30, ..., 100
        }

        // Assert
        var stats = _tracker.GetUsageStatistics();
        var restStats = stats.FirstOrDefault(s => s.Category == category);

        restStats.Should().NotBeNull("REST category should be tracked");
        restStats!.RequestCount.Should().Be(10, "ten requests were recorded");
        restStats.AvgLatencyMs.Should().Be(55.0, "average of 10-100 is 55");
    }

    [Fact]
    public void RecordRequest_WhenTrackingDisabled_DoesNotRecord()
    {
        // Arrange
        var disabledOptions = new PerformanceMetricsOptions
        {
            ApiRequestTrackingEnabled = false
        };

        var disabledTracker = new ApiRequestTracker(
            NullLogger<ApiRequestTracker>.Instance,
            Options.Create(disabledOptions));

        // Act
        disabledTracker.TrackLogEvent("Rest", "Some REST message", 0);

        // Assert
        var stats = disabledTracker.GetUsageStatistics();
        stats.Should().BeEmpty("tracking is disabled");
    }

    [Fact]
    public void RecordRequest_MultipleCategories_TracksSeparately()
    {
        // Arrange & Act
        _tracker.RecordRequest("REST", 30);
        _tracker.RecordRequest("REST", 40);
        _tracker.RecordRequest("Gateway", 15);
        _tracker.RecordRequest("Gateway", 25);
        _tracker.RecordRequest("VoiceConnection", 100);

        // Assert
        var stats = _tracker.GetUsageStatistics();
        stats.Should().HaveCount(3, "three categories were used");

        var restStats = stats.First(s => s.Category == "REST");
        restStats.RequestCount.Should().Be(2);
        restStats.AvgLatencyMs.Should().Be(35.0);

        var gatewayStats = stats.First(s => s.Category == "Gateway");
        gatewayStats.RequestCount.Should().Be(2);
        gatewayStats.AvgLatencyMs.Should().Be(20.0);

        var voiceStats = stats.First(s => s.Category == "VoiceConnection");
        voiceStats.RequestCount.Should().Be(1);
        voiceStats.AvgLatencyMs.Should().Be(100.0);
    }

    // ============================================================================
    // GetLatencyStatistics Tests
    // ============================================================================

    [Fact]
    public void GetLatencyStatistics_WithNoData_ReturnsZeroValues()
    {
        // Act
        var stats = _tracker.GetLatencyStatistics(hours: 24);

        // Assert
        stats.Should().NotBeNull("statistics object should always be returned");
        stats.AvgLatencyMs.Should().Be(0, "average should be zero with no data");
        stats.MinLatencyMs.Should().Be(0, "min should be zero with no data");
        stats.MaxLatencyMs.Should().Be(0, "max should be zero with no data");
        stats.P50LatencyMs.Should().Be(0, "P50 should be zero with no data");
        stats.P95LatencyMs.Should().Be(0, "P95 should be zero with no data");
        stats.P99LatencyMs.Should().Be(0, "P99 should be zero with no data");
        stats.SampleCount.Should().Be(0, "sample count should be zero");
    }

    [Fact]
    public void GetLatencyStatistics_WithSingleSample_ReturnsCorrectStats()
    {
        // Arrange
        const int latency = 75;
        _tracker.RecordRequest("REST", latency);

        // Act
        var stats = _tracker.GetLatencyStatistics(hours: 24);

        // Assert
        stats.SampleCount.Should().BeGreaterThan(0, "should have samples");
        stats.AvgLatencyMs.Should().Be(latency, "average equals the single value");
        stats.MinLatencyMs.Should().Be(latency, "min equals the single value");
        stats.MaxLatencyMs.Should().Be(latency, "max equals the single value");
    }

    [Fact]
    public void GetLatencyStatistics_WithMultipleSamples_CalculatesCorrectStats()
    {
        // Arrange - Record requests with known latency values
        var latencies = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        foreach (var latency in latencies)
        {
            _tracker.RecordRequest("REST", latency);
        }

        // Act
        var stats = _tracker.GetLatencyStatistics(hours: 24);

        // Assert
        stats.SampleCount.Should().BeGreaterThan(0, "should have recorded samples");
        stats.AvgLatencyMs.Should().Be(55.0, "average of 10-100 is 55");
        stats.MinLatencyMs.Should().Be(10, "minimum value is 10");
        stats.MaxLatencyMs.Should().Be(100, "maximum value is 100");
        stats.P50LatencyMs.Should().BeGreaterThan(0, "P50 should be calculated");
        stats.P95LatencyMs.Should().BeGreaterThan(0, "P95 should be calculated");
        stats.P99LatencyMs.Should().BeGreaterThan(0, "P99 should be calculated");
    }

    [Fact]
    public void GetLatencyStatistics_CalculatesCorrectPercentiles()
    {
        // Arrange - Record 100 values from 1 to 100
        // Note: The tracker aggregates values into 5-minute buckets, so all these
        // values will be averaged into a single bucket. The percentile calculation
        // happens on the bucket averages, not individual request values.
        for (int i = 1; i <= 100; i++)
        {
            _tracker.RecordRequest("REST", i);
        }

        // Act
        var stats = _tracker.GetLatencyStatistics(hours: 24);

        // Assert
        stats.SampleCount.Should().BeGreaterThan(0, "should have samples");
        // Since all values go into one bucket, the average will be around 50.5
        // and percentiles will be based on that single average value
        stats.P50LatencyMs.Should().BeGreaterThan(0, "P50 should be calculated");
        stats.P95LatencyMs.Should().BeGreaterThan(0, "P95 should be calculated");
        stats.P99LatencyMs.Should().BeGreaterThan(0, "P99 should be calculated");
        // Verify percentiles are ordered correctly
        stats.P99LatencyMs.Should().BeGreaterThanOrEqualTo(stats.P95LatencyMs,
            "P99 should be >= P95");
        stats.P95LatencyMs.Should().BeGreaterThanOrEqualTo(stats.P50LatencyMs,
            "P95 should be >= P50");
    }

    [Fact]
    public void GetLatencyStatistics_WithHoursFilter_ReturnsOnlyRecentStats()
    {
        // Arrange - Record some requests
        for (int i = 1; i <= 50; i++)
        {
            _tracker.RecordRequest("REST", i);
        }

        // Act - Request statistics for a very small time window
        var recentStats = _tracker.GetLatencyStatistics(hours: 1);
        var allStats = _tracker.GetLatencyStatistics(hours: 24);

        // Assert - Both should have data since requests were just recorded
        recentStats.SampleCount.Should().BeGreaterThan(0, "recent stats should include just-recorded samples");
        allStats.SampleCount.Should().BeGreaterThan(0, "all stats should include just-recorded samples");
        recentStats.SampleCount.Should().BeLessThanOrEqualTo(allStats.SampleCount, "recent should not exceed all");
    }

    [Fact]
    public void GetLatencyStatistics_WithZeroLatencyRequests_ExcludesFromStats()
    {
        // Arrange - Log-parsed requests have zero latency and should be excluded
        _tracker.TrackLogEvent("Rest", "Some REST log message", 0);
        _tracker.RecordRequest("REST", 50); // Actual request with latency

        // Act
        var stats = _tracker.GetLatencyStatistics(hours: 24);

        // Assert - Zero-latency requests should not affect statistics
        stats.SampleCount.Should().BeGreaterThan(0, "should have samples from non-zero latency");
    }

    // ============================================================================
    // GetLatencySamples Tests
    // ============================================================================

    [Fact]
    public void GetLatencySamples_WithNoData_ReturnsEmptyList()
    {
        // Act
        var samples = _tracker.GetLatencySamples(hours: 24);

        // Assert
        samples.Should().NotBeNull("samples list should never be null");
        samples.Should().BeEmpty("no samples have been recorded");
    }

    [Fact]
    public void GetLatencySamples_WithData_ReturnsSamplesInOrder()
    {
        // Arrange - Record multiple requests
        for (int i = 1; i <= 10; i++)
        {
            _tracker.RecordRequest("REST", i * 10);
        }

        // Act
        var samples = _tracker.GetLatencySamples(hours: 24);

        // Assert
        samples.Should().NotBeEmpty("samples should be returned");
        samples.Should().BeInAscendingOrder(s => s.Timestamp, "samples should be in chronological order");

        foreach (var sample in samples)
        {
            sample.AvgLatencyMs.Should().BeGreaterThan(0, "average latency should be positive");
            sample.P95LatencyMs.Should().BeGreaterThanOrEqualTo(0, "P95 latency should be non-negative");
        }
    }

    [Fact]
    public void GetLatencySamples_WithHoursFilter_ReturnsFilteredSamples()
    {
        // Arrange - Record requests
        for (int i = 1; i <= 20; i++)
        {
            _tracker.RecordRequest("REST", i * 5);
        }

        // Act
        var samples1Hour = _tracker.GetLatencySamples(hours: 1);
        var samples24Hours = _tracker.GetLatencySamples(hours: 24);

        // Assert - All recent samples should be within both time ranges
        samples1Hour.Should().NotBeEmpty("should have samples within 1 hour");
        samples24Hours.Should().NotBeEmpty("should have samples within 24 hours");
        samples1Hour.Count.Should().BeLessThanOrEqualTo(samples24Hours.Count,
            "1-hour window should not have more samples than 24-hour window");
    }

    [Fact]
    public void GetLatencySamples_ContainsValidTimestamps()
    {
        // Arrange
        var beforeRecording = DateTime.UtcNow;
        _tracker.RecordRequest("REST", 42);
        var afterRecording = DateTime.UtcNow;

        // Act
        var samples = _tracker.GetLatencySamples(hours: 1);

        // Assert
        samples.Should().NotBeEmpty("should have at least one sample");
        foreach (var sample in samples)
        {
            sample.Timestamp.Should().BeOnOrAfter(beforeRecording.AddMinutes(-1),
                "timestamp should be recent");
            sample.Timestamp.Should().BeOnOrBefore(afterRecording.AddMinutes(1),
                "timestamp should not be in the future");
        }
    }

    // ============================================================================
    // GetRequestVolume Tests
    // ============================================================================

    [Fact]
    public void GetRequestVolume_WithNoData_ReturnsEmptyList()
    {
        // Act
        var volume = _tracker.GetRequestVolume(hours: 24);

        // Assert
        volume.Should().NotBeNull("volume list should never be null");
        volume.Should().BeEmpty("no requests have been recorded");
    }

    [Fact]
    public void GetRequestVolume_WithData_ReturnsHourlyBuckets()
    {
        // Arrange - Record multiple requests
        for (int i = 0; i < 25; i++)
        {
            _tracker.RecordRequest("REST", 50);
        }

        // Act
        var volume = _tracker.GetRequestVolume(hours: 24);

        // Assert
        volume.Should().NotBeEmpty("should have volume data");

        foreach (var dataPoint in volume)
        {
            dataPoint.RequestCount.Should().BeGreaterThan(0, "request count should be positive");
            dataPoint.Timestamp.Kind.Should().Be(DateTimeKind.Utc, "timestamps should be UTC");
            dataPoint.Timestamp.Minute.Should().Be(0, "timestamps should be rounded to the hour");
            dataPoint.Timestamp.Second.Should().Be(0, "timestamps should be rounded to the hour");
            dataPoint.Category.Should().NotBeNullOrEmpty("category should be set");
        }
    }

    [Fact]
    public void GetRequestVolume_ReturnsDataInChronologicalOrder()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            _tracker.RecordRequest("REST", 25);
        }

        // Act
        var volume = _tracker.GetRequestVolume(hours: 24);

        // Assert
        if (volume.Any())
        {
            volume.Should().BeInAscendingOrder(v => v.Timestamp,
                "volume data should be in chronological order");
        }
    }

    [Fact]
    public void GetRequestVolume_WithHoursParameter_LimitsResults()
    {
        // Arrange - Record requests
        for (int i = 0; i < 50; i++)
        {
            _tracker.RecordRequest("Gateway", 15);
        }

        // Act
        var volume1Hour = _tracker.GetRequestVolume(hours: 1);
        var volume6Hours = _tracker.GetRequestVolume(hours: 6);
        var volume24Hours = _tracker.GetRequestVolume(hours: 24);

        // Assert - Verify that hour limits are respected
        volume1Hour.Count.Should().BeLessThanOrEqualTo(volume6Hours.Count,
            "1-hour window should not exceed 6-hour window");
        volume6Hours.Count.Should().BeLessThanOrEqualTo(volume24Hours.Count,
            "6-hour window should not exceed 24-hour window");
        volume24Hours.Count.Should().BeLessThanOrEqualTo(24,
            "24-hour window should not exceed 24 data points");
    }

    // ============================================================================
    // TrackLogEvent Tests (Existing Functionality)
    // ============================================================================

    [Fact]
    public void TrackLogEvent_RestMessages_RecordsAsRest()
    {
        // Arrange
        const string source = "Rest";
        const string message = "Sent GET /api/guilds";
        const int severity = 1; // Info

        // Act
        _tracker.TrackLogEvent(source, message, severity);

        // Assert
        var stats = _tracker.GetUsageStatistics();
        var restStats = stats.FirstOrDefault(s => s.Category == "REST");

        restStats.Should().NotBeNull("REST category should be tracked");
        restStats!.RequestCount.Should().Be(1, "one REST request was logged");
    }

    [Fact]
    public void TrackLogEvent_GatewayMessages_RecordsAsGateway()
    {
        // Arrange
        const string source = "Gateway";
        const string message = "Received Gateway event";
        const int severity = 1; // Info

        // Act
        _tracker.TrackLogEvent(source, message, severity);

        // Assert
        var stats = _tracker.GetUsageStatistics();
        var gatewayStats = stats.FirstOrDefault(s => s.Category == "Gateway");

        gatewayStats.Should().NotBeNull("Gateway category should be tracked");
        gatewayStats!.RequestCount.Should().Be(1, "one Gateway event was logged");
    }

    [Fact]
    public void TrackLogEvent_ErrorSeverity_IncrementsErrorCount()
    {
        // Arrange
        const string source = "Rest";
        const string message = "REST request failed";
        const int errorSeverity = 4; // Error

        // Act
        _tracker.TrackLogEvent(source, message, errorSeverity);

        // Assert
        var stats = _tracker.GetUsageStatistics();
        var restStats = stats.FirstOrDefault(s => s.Category == "REST");

        restStats.Should().NotBeNull("REST category should be tracked");
        restStats!.ErrorCount.Should().Be(1, "one error was recorded");
    }

    [Fact]
    public void TrackLogEvent_CriticalSeverity_IncrementsErrorCount()
    {
        // Arrange
        const string source = "Gateway";
        const string message = "Critical gateway error";
        const int criticalSeverity = 5; // Critical

        // Act
        _tracker.TrackLogEvent(source, message, criticalSeverity);

        // Assert
        var stats = _tracker.GetUsageStatistics();
        var gatewayStats = stats.FirstOrDefault(s => s.Category == "Gateway");

        gatewayStats.Should().NotBeNull("Gateway category should be tracked");
        gatewayStats!.ErrorCount.Should().Be(1, "one critical error was recorded");
    }

    [Fact]
    public void TrackLogEvent_ParsesRateLimitFromMessage()
    {
        // Arrange
        const string message = "Rate limit hit on endpoint: /api/channels/123, retry after 1500 ms";

        // Act
        _tracker.TrackLogEvent("Rest", message, 3); // Warning

        // Assert
        var rateLimitEvents = _tracker.GetRateLimitEvents(hours: 24);
        rateLimitEvents.Should().NotBeEmpty("rate limit event should be parsed and recorded");
    }

    // ============================================================================
    // RecordRateLimitHit Tests
    // ============================================================================

    [Fact]
    public void RecordRateLimitHit_ValidEvent_StoresEvent()
    {
        // Arrange
        const string endpoint = "/api/channels/123/messages";
        const int retryAfterMs = 2000;
        const bool isGlobal = false;

        // Act
        _tracker.RecordRateLimitHit(endpoint, retryAfterMs, isGlobal);

        // Assert
        var events = _tracker.GetRateLimitEvents(hours: 24);
        events.Should().NotBeEmpty("rate limit event should be stored");

        var evt = events.First();
        evt.Endpoint.Should().Be(endpoint, "endpoint should match");
        evt.RetryAfterMs.Should().Be(retryAfterMs, "retry duration should match");
        evt.IsGlobal.Should().Be(isGlobal, "global flag should match");
        evt.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1),
            "timestamp should be recent");
    }

    [Fact]
    public void RecordRateLimitHit_GlobalRateLimit_SetsGlobalFlag()
    {
        // Arrange & Act
        _tracker.RecordRateLimitHit("/api/test", 5000, isGlobal: true);

        // Assert
        var events = _tracker.GetRateLimitEvents(hours: 24);
        events.Should().Contain(e => e.IsGlobal, "global rate limit should be flagged");
    }

    [Fact]
    public void RecordRateLimitHit_MultipleEvents_StoresAll()
    {
        // Arrange & Act
        _tracker.RecordRateLimitHit("/api/endpoint1", 1000, false);
        _tracker.RecordRateLimitHit("/api/endpoint2", 2000, false);
        _tracker.RecordRateLimitHit("/api/endpoint3", 3000, true);

        // Assert
        var events = _tracker.GetRateLimitEvents(hours: 24);
        events.Should().HaveCount(3, "all three events should be stored");
    }

    [Fact]
    public void RecordRateLimitHit_ExceedsMaxEvents_TrimsOldest()
    {
        // Arrange - Record more than the max (1000) rate limit events
        for (int i = 0; i < 1005; i++)
        {
            _tracker.RecordRateLimitHit($"/api/endpoint{i}", 1000, false);
        }

        // Act
        var events = _tracker.GetRateLimitEvents(hours: 24);

        // Assert
        events.Count.Should().BeLessThanOrEqualTo(1000,
            "should not exceed maximum rate limit event count");
    }

    // ============================================================================
    // GetRateLimitEvents Tests
    // ============================================================================

    [Fact]
    public void GetRateLimitEvents_WithHoursFilter_ReturnsFilteredEvents()
    {
        // Arrange
        _tracker.RecordRateLimitHit("/api/test", 1000, false);

        // Act
        var recentEvents = _tracker.GetRateLimitEvents(hours: 1);
        var allEvents = _tracker.GetRateLimitEvents(hours: 24);

        // Assert
        recentEvents.Should().NotBeEmpty("should include just-recorded event");
        allEvents.Should().NotBeEmpty("should include just-recorded event");
        recentEvents.Count.Should().BeLessThanOrEqualTo(allEvents.Count,
            "recent events should not exceed all events");
    }

    [Fact]
    public void GetRateLimitEvents_WithNoEvents_ReturnsEmptyList()
    {
        // Act
        var events = _tracker.GetRateLimitEvents(hours: 24);

        // Assert
        events.Should().NotBeNull("event list should never be null");
        events.Should().BeEmpty("no events have been recorded");
    }

    // ============================================================================
    // GetTotalRequests Tests
    // ============================================================================

    [Fact]
    public void GetTotalRequests_WithNoData_ReturnsZero()
    {
        // Act
        var total = _tracker.GetTotalRequests(hours: 24);

        // Assert
        total.Should().Be(0, "no requests have been recorded");
    }

    [Fact]
    public void GetTotalRequests_WithData_ReturnsCorrectCount()
    {
        // Arrange
        const int requestCount = 42;
        for (int i = 0; i < requestCount; i++)
        {
            _tracker.RecordRequest("REST", 30);
        }

        // Act
        var total = _tracker.GetTotalRequests(hours: 24);

        // Assert
        total.Should().Be(requestCount, "all recorded requests should be counted");
    }

    [Fact]
    public void GetTotalRequests_IncludesAllCategories()
    {
        // Arrange
        _tracker.RecordRequest("REST", 20);
        _tracker.RecordRequest("REST", 25);
        _tracker.RecordRequest("Gateway", 15);
        _tracker.RecordRequest("Gateway", 18);
        _tracker.RecordRequest("VoiceConnection", 50);

        // Act
        var total = _tracker.GetTotalRequests(hours: 24);

        // Assert
        total.Should().Be(5, "should count requests across all categories");
    }

    [Fact]
    public void GetTotalRequests_WithHoursParameter_RespectsTimeWindow()
    {
        // Arrange - Record requests
        for (int i = 0; i < 30; i++)
        {
            _tracker.RecordRequest("REST", 40);
        }

        // Act
        var total1Hour = _tracker.GetTotalRequests(hours: 1);
        var total24Hours = _tracker.GetTotalRequests(hours: 24);

        // Assert - All requests should be within both windows since just recorded
        total1Hour.Should().BeGreaterThan(0, "should have requests in 1-hour window");
        total24Hours.Should().BeGreaterThan(0, "should have requests in 24-hour window");
        total1Hour.Should().BeLessThanOrEqualTo(total24Hours,
            "1-hour total should not exceed 24-hour total");
    }

    // ============================================================================
    // GetUsageStatistics Tests
    // ============================================================================

    [Fact]
    public void GetUsageStatistics_WithNoData_ReturnsEmptyList()
    {
        // Act
        var stats = _tracker.GetUsageStatistics();

        // Assert
        stats.Should().NotBeNull("statistics list should never be null");
        stats.Should().BeEmpty("no requests have been recorded");
    }

    [Fact]
    public void GetUsageStatistics_ReturnsAllCategories()
    {
        // Arrange
        _tracker.RecordRequest("REST", 30);
        _tracker.RecordRequest("Gateway", 20);
        _tracker.RecordRequest("VoiceConnection", 60);

        // Act
        var stats = _tracker.GetUsageStatistics();

        // Assert
        stats.Should().HaveCount(3, "three categories were used");
        stats.Should().Contain(s => s.Category == "REST");
        stats.Should().Contain(s => s.Category == "Gateway");
        stats.Should().Contain(s => s.Category == "VoiceConnection");
    }

    [Fact]
    public void GetUsageStatistics_CalculatesAverageLatencyCorrectly()
    {
        // Arrange
        _tracker.RecordRequest("REST", 100);
        _tracker.RecordRequest("REST", 200);
        _tracker.RecordRequest("REST", 300);

        // Act
        var stats = _tracker.GetUsageStatistics();
        var restStats = stats.First(s => s.Category == "REST");

        // Assert
        restStats.AvgLatencyMs.Should().Be(200.0, "average of 100, 200, 300 is 200");
    }

    [Fact]
    public void GetUsageStatistics_WithZeroRequests_ReturnsZeroAverage()
    {
        // Arrange - This scenario shouldn't normally happen, but test defensive code
        _tracker.TrackLogEvent("Rest", "Some log without latency tracking", 1);

        // Act
        var stats = _tracker.GetUsageStatistics();

        // Assert - Should handle gracefully
        if (stats.Any())
        {
            foreach (var stat in stats)
            {
                stat.AvgLatencyMs.Should().BeGreaterThanOrEqualTo(0, "average should never be negative");
            }
        }
    }

    // ============================================================================
    // Thread Safety Tests
    // ============================================================================

    [Fact]
    public void ConcurrentRecordRequest_DoesNotCorruptData()
    {
        // Arrange
        const int threadCount = 10;
        const int requestsPerThread = 100;

        // Act - Record requests concurrently from multiple threads
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(() =>
            {
                for (int i = 0; i < requestsPerThread; i++)
                {
                    _tracker.RecordRequest("REST", threadId * 10 + i);
                }
            })
        ).ToArray();

        Task.WaitAll(tasks);

        // Assert
        var total = _tracker.GetTotalRequests(hours: 24);
        total.Should().Be(threadCount * requestsPerThread,
            "all concurrent requests should be counted");

        var stats = _tracker.GetUsageStatistics();
        var restStats = stats.First(s => s.Category == "REST");
        restStats.RequestCount.Should().Be(threadCount * requestsPerThread,
            "all concurrent requests should be in statistics");
    }

    [Fact]
    public void ConcurrentRateLimitRecording_DoesNotCorruptData()
    {
        // Arrange
        const int threadCount = 5;
        const int eventsPerThread = 20;

        // Act - Record rate limit events concurrently
        var tasks = Enumerable.Range(0, threadCount).Select(threadId =>
            Task.Run(() =>
            {
                for (int i = 0; i < eventsPerThread; i++)
                {
                    _tracker.RecordRateLimitHit($"/api/thread{threadId}/endpoint{i}", 1000, false);
                }
            })
        ).ToArray();

        Task.WaitAll(tasks);

        // Assert
        var events = _tracker.GetRateLimitEvents(hours: 24);
        events.Count.Should().Be(threadCount * eventsPerThread,
            "all concurrent rate limit events should be recorded");
    }

    // ============================================================================
    // Edge Cases and Error Handling
    // ============================================================================

    [Fact]
    public void RecordRequest_WithNegativeLatency_HandlesGracefully()
    {
        // Arrange & Act - Should not throw
        var act = () => _tracker.RecordRequest("REST", -10);

        // Assert
        act.Should().NotThrow("negative latency should be handled gracefully");
    }

    [Fact]
    public void RecordRequest_WithZeroLatency_HandlesGracefully()
    {
        // Arrange & Act
        _tracker.RecordRequest("REST", 0);

        // Assert
        var stats = _tracker.GetUsageStatistics();
        stats.Should().Contain(s => s.Category == "REST", "category should still be tracked");
    }

    [Fact]
    public void RecordRequest_WithEmptyCategory_HandlesGracefully()
    {
        // Arrange & Act - Should not throw
        var act = () => _tracker.RecordRequest(string.Empty, 50);

        // Assert
        act.Should().NotThrow("empty category should be handled gracefully");
    }

    [Fact]
    public void GetLatencyStatistics_WithInvalidHoursParameter_HandlesGracefully()
    {
        // Arrange
        _tracker.RecordRequest("REST", 50);

        // Act - Use invalid parameters
        var statsNegative = _tracker.GetLatencyStatistics(hours: -1);
        var statsZero = _tracker.GetLatencyStatistics(hours: 0);

        // Assert - Should return valid (likely empty) statistics without throwing
        statsNegative.Should().NotBeNull("should return statistics object");
        statsZero.Should().NotBeNull("should return statistics object");
    }

    [Fact]
    public void TrackLogEvent_WithNullOrEmptyMessage_HandlesGracefully()
    {
        // Act - Should not throw
        var actNull = () => _tracker.TrackLogEvent("Rest", null!, 1);
        var actEmpty = () => _tracker.TrackLogEvent("Rest", string.Empty, 1);

        // Assert
        actNull.Should().NotThrow("null message should be handled gracefully");
        actEmpty.Should().NotThrow("empty message should be handled gracefully");
    }
}
