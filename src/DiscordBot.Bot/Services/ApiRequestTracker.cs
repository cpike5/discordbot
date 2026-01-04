using System.Collections.Concurrent;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;
using DiscordBot.Core.Configuration;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for tracking Discord API requests and rate limiting events from Discord.NET log messages.
/// Thread-safe singleton service that parses log events for API usage patterns.
/// </summary>
public class ApiRequestTracker : IApiRequestTracker, IMemoryReportable
{
    private readonly ILogger<ApiRequestTracker> _logger;
    private readonly PerformanceMetricsOptions _options;
    private readonly object _lock = new();

    // Track request counts by category
    private readonly ConcurrentDictionary<string, ApiCategory> _categories = new();

    // Track rate limit events
    private readonly ConcurrentQueue<RateLimitEvent> _rateLimitEvents = new();
    private int _rateLimitEventCount;
    private const int MaxRateLimitEvents = 1000;

    // Hourly request buckets for the last 24 hours
    private readonly long[] _hourlyRequests = new long[24];
    private int _currentHourIndex;
    private DateTime _lastHourUpdate = DateTime.UtcNow;

    // Latency tracking with circular buffer (store 5-minute buckets for 24 hours = 288 samples)
    private readonly LatencySample[] _latencySamples = new LatencySample[288];
    private int _latencyIndex;
    private int _latencySampleCount;
    private DateTime _lastLatencySampleTime = DateTime.UtcNow;

    // Severity level mapping
    private const int SeverityError = 4; // LogSeverity.Error
    private const int SeverityCritical = 5; // LogSeverity.Critical

    public ApiRequestTracker(
        ILogger<ApiRequestTracker> logger,
        IOptions<PerformanceMetricsOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        _currentHourIndex = DateTime.UtcNow.Hour;
    }

    /// <inheritdoc/>
    public void TrackLogEvent(string source, string message, int severity)
    {
        if (!_options.ApiRequestTrackingEnabled)
        {
            return;
        }

        try
        {
            // Update hourly bucket
            UpdateHourlyBucket();

            // Parse different types of Discord.NET log messages
            if (source.Contains("Rest", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("REST", StringComparison.OrdinalIgnoreCase))
            {
                RecordRequestFromLog("REST", severity);
                _logger.LogTrace("Tracked REST API request: {Message}", message);
            }
            else if (source.Contains("Gateway", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("Gateway", StringComparison.OrdinalIgnoreCase))
            {
                RecordRequestFromLog("Gateway", severity);
                _logger.LogTrace("Tracked Gateway event: {Message}", message);
            }

            // Check for rate limit indicators
            if (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("429", StringComparison.OrdinalIgnoreCase))
            {
                ParseRateLimitEvent(message);
            }
        }
        catch (Exception ex)
        {
            // Don't let tracking errors break the application
            _logger.LogWarning(ex, "Error tracking API request from log message");
        }
    }

    /// <inheritdoc/>
    public void RecordRateLimitHit(string endpoint, int retryAfterMs, bool isGlobal)
    {
        var evt = new RateLimitEvent
        {
            Timestamp = DateTime.UtcNow,
            Endpoint = endpoint,
            RetryAfterMs = retryAfterMs,
            IsGlobal = isGlobal
        };

        _rateLimitEvents.Enqueue(evt);
        Interlocked.Increment(ref _rateLimitEventCount);

        // Trim to max events
        while (_rateLimitEventCount > MaxRateLimitEvents)
        {
            if (_rateLimitEvents.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _rateLimitEventCount);
            }
        }

        _logger.LogWarning(
            "Rate limit hit: Endpoint={Endpoint}, RetryAfter={RetryAfterMs}ms, IsGlobal={IsGlobal}",
            endpoint, retryAfterMs, isGlobal);
    }

    /// <inheritdoc/>
    public IReadOnlyList<ApiUsageDto> GetUsageStatistics(int hours = 24)
    {
        return _categories.Select(kvp => new ApiUsageDto
        {
            Category = kvp.Key,
            RequestCount = kvp.Value.RequestCount,
            AvgLatencyMs = kvp.Value.TotalLatencyMs > 0 && kvp.Value.RequestCount > 0
                ? (double)kvp.Value.TotalLatencyMs / kvp.Value.RequestCount
                : 0,
            ErrorCount = kvp.Value.ErrorCount
        }).ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<RateLimitEventDto> GetRateLimitEvents(int hours = 24)
    {
        var cutoff = DateTime.UtcNow.AddHours(-hours);

        return _rateLimitEvents
            .Where(e => e.Timestamp >= cutoff)
            .Select(e => new RateLimitEventDto
            {
                Timestamp = e.Timestamp,
                Endpoint = e.Endpoint,
                RetryAfterMs = e.RetryAfterMs,
                IsGlobal = e.IsGlobal
            })
            .ToList();
    }

    /// <inheritdoc/>
    public long GetTotalRequests(int hours = 24)
    {
        lock (_lock)
        {
            UpdateHourlyBucket();

            // Sum up the requested number of hours
            var hoursToSum = Math.Min(hours, 24);
            long total = 0;

            for (int i = 0; i < hoursToSum; i++)
            {
                var index = (_currentHourIndex - i + 24) % 24;
                total += _hourlyRequests[index];
            }

            return total;
        }
    }

    /// <inheritdoc/>
    public void RecordRequest(string category, int latencyMs)
    {
        var apiCategory = _categories.GetOrAdd(category, _ => new ApiCategory());

        Interlocked.Increment(ref apiCategory.RequestCount);
        Interlocked.Add(ref apiCategory.TotalLatencyMs, latencyMs);

        lock (_lock)
        {
            UpdateHourlyBucket();
            _hourlyRequests[_currentHourIndex]++;
            UpdateLatencySample(latencyMs);
        }

        _logger.LogTrace("Recorded API request: Category={Category}, Latency={LatencyMs}ms", category, latencyMs);
    }

    /// <summary>
    /// Records an API request for a specific category from log parsing (legacy method).
    /// </summary>
    private void RecordRequestFromLog(string category, int severity)
    {
        var apiCategory = _categories.GetOrAdd(category, _ => new ApiCategory());

        Interlocked.Increment(ref apiCategory.RequestCount);

        if (severity == SeverityError || severity == SeverityCritical)
        {
            Interlocked.Increment(ref apiCategory.ErrorCount);
        }

        lock (_lock)
        {
            UpdateHourlyBucket();
            _hourlyRequests[_currentHourIndex]++;
            // Use a default latency for log-parsed requests
            UpdateLatencySample(0);
        }
    }

    /// <summary>
    /// Updates the hourly request bucket if we've moved to a new hour.
    /// </summary>
    private void UpdateHourlyBucket()
    {
        var now = DateTime.UtcNow;
        var currentHour = now.Hour;

        if (currentHour != _currentHourIndex)
        {
            // Reset buckets for hours that have passed
            var hoursElapsed = (currentHour - _currentHourIndex + 24) % 24;
            for (int i = 0; i < hoursElapsed; i++)
            {
                var indexToReset = (_currentHourIndex + i + 1) % 24;
                _hourlyRequests[indexToReset] = 0;
            }

            _currentHourIndex = currentHour;
            _lastHourUpdate = now;
        }
    }

    /// <summary>
    /// Attempts to parse rate limit information from a log message.
    /// </summary>
    private void ParseRateLimitEvent(string message)
    {
        try
        {
            // Try to extract endpoint and retry-after from the message
            // This is best-effort parsing since Discord.NET log format may vary
            var endpoint = "Unknown";
            var retryAfter = 1000; // Default to 1 second
            var isGlobal = message.Contains("global", StringComparison.OrdinalIgnoreCase);

            // Look for patterns like "endpoint: /api/..." or similar
            var endpointMatch = System.Text.RegularExpressions.Regex.Match(message, @"endpoint[:\s]+([^\s,]+)");
            if (endpointMatch.Success)
            {
                endpoint = endpointMatch.Groups[1].Value;
            }

            // Look for retry-after duration
            var retryMatch = System.Text.RegularExpressions.Regex.Match(message, @"(\d+)\s*ms");
            if (retryMatch.Success && int.TryParse(retryMatch.Groups[1].Value, out var parsedRetry))
            {
                retryAfter = parsedRetry;
            }

            RecordRateLimitHit(endpoint, retryAfter, isGlobal);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse rate limit event from message: {Message}", message);
        }
    }

    /// <summary>
    /// Updates the latency sample buffer with a new measurement.
    /// Aggregates samples into 5-minute buckets.
    /// </summary>
    private void UpdateLatencySample(int latencyMs)
    {
        var now = DateTime.UtcNow;
        var timeSinceLastSample = (now - _lastLatencySampleTime).TotalMinutes;

        // Create a new 5-minute bucket if needed
        if (timeSinceLastSample >= 5 || _latencySampleCount == 0)
        {
            var sample = new LatencySample
            {
                Timestamp = now,
                TotalLatencyMs = latencyMs,
                Count = latencyMs > 0 ? 1 : 0, // Don't count zero-latency (log-parsed) requests
                MinLatencyMs = latencyMs > 0 ? latencyMs : int.MaxValue,
                MaxLatencyMs = latencyMs
            };

            _latencySamples[_latencyIndex] = sample;
            _latencyIndex = (_latencyIndex + 1) % _latencySamples.Length;

            if (_latencySampleCount < _latencySamples.Length)
            {
                _latencySampleCount++;
            }

            _lastLatencySampleTime = now;
        }
        else if (latencyMs > 0)
        {
            // Update the current bucket
            var currentIndex = (_latencyIndex - 1 + _latencySamples.Length) % _latencySamples.Length;
            var sample = _latencySamples[currentIndex];
            sample.TotalLatencyMs += latencyMs;
            sample.Count++;
            sample.MinLatencyMs = Math.Min(sample.MinLatencyMs, latencyMs);
            sample.MaxLatencyMs = Math.Max(sample.MaxLatencyMs, latencyMs);
            _latencySamples[currentIndex] = sample;
        }
    }

    /// <inheritdoc/>
    public ApiLatencyStatsDto GetLatencyStatistics(int hours = 24)
    {
        lock (_lock)
        {
            if (_latencySampleCount == 0)
            {
                return new ApiLatencyStatsDto
                {
                    AvgLatencyMs = 0,
                    MinLatencyMs = 0,
                    MaxLatencyMs = 0,
                    P50LatencyMs = 0,
                    P95LatencyMs = 0,
                    P99LatencyMs = 0,
                    SampleCount = 0
                };
            }

            var cutoff = DateTime.UtcNow.AddHours(-hours);
            var values = new List<double>();
            var minLatency = double.MaxValue;
            var maxLatency = 0.0;
            var totalLatency = 0.0;
            var totalCount = 0;

            // Collect latency values from samples within the time window
            var startIndex = _latencySampleCount < _latencySamples.Length ? 0 : _latencyIndex;
            for (int i = 0; i < _latencySampleCount; i++)
            {
                var index = (startIndex + i) % _latencySamples.Length;
                var sample = _latencySamples[index];

                if (sample.Timestamp >= cutoff && sample.Count > 0)
                {
                    var avgLatency = (double)sample.TotalLatencyMs / sample.Count;
                    values.Add(avgLatency);
                    minLatency = Math.Min(minLatency, sample.MinLatencyMs);
                    maxLatency = Math.Max(maxLatency, sample.MaxLatencyMs);
                    totalLatency += sample.TotalLatencyMs;
                    totalCount += sample.Count;
                }
            }

            if (values.Count == 0 || totalCount == 0)
            {
                return new ApiLatencyStatsDto
                {
                    AvgLatencyMs = 0,
                    MinLatencyMs = 0,
                    MaxLatencyMs = 0,
                    P50LatencyMs = 0,
                    P95LatencyMs = 0,
                    P99LatencyMs = 0,
                    SampleCount = 0
                };
            }

            // Sort for percentile calculation
            values.Sort();

            var stats = new ApiLatencyStatsDto
            {
                AvgLatencyMs = totalLatency / totalCount,
                MinLatencyMs = minLatency,
                MaxLatencyMs = maxLatency,
                P50LatencyMs = CalculatePercentile(values, 50),
                P95LatencyMs = CalculatePercentile(values, 95),
                P99LatencyMs = CalculatePercentile(values, 99),
                SampleCount = totalCount
            };

            _logger.LogDebug(
                "Calculated API latency statistics for {Hours}h: Avg={Avg:F1}ms, P50={P50:F1}ms, P95={P95:F1}ms, P99={P99:F1}ms, Samples={Count}",
                hours, stats.AvgLatencyMs, stats.P50LatencyMs, stats.P95LatencyMs, stats.P99LatencyMs, stats.SampleCount);

            return stats;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<ApiLatencySampleDto> GetLatencySamples(int hours = 24)
    {
        lock (_lock)
        {
            if (_latencySampleCount == 0)
            {
                return Array.Empty<ApiLatencySampleDto>();
            }

            var cutoff = DateTime.UtcNow.AddHours(-hours);
            var result = new List<ApiLatencySampleDto>();

            // Read samples in chronological order
            var startIndex = _latencySampleCount < _latencySamples.Length ? 0 : _latencyIndex;
            for (int i = 0; i < _latencySampleCount; i++)
            {
                var index = (startIndex + i) % _latencySamples.Length;
                var sample = _latencySamples[index];

                if (sample.Timestamp >= cutoff && sample.Count > 0)
                {
                    var avgLatency = (double)sample.TotalLatencyMs / sample.Count;

                    // For P95, we'll use the max latency in the bucket as an approximation
                    var p95Latency = sample.MaxLatencyMs;

                    result.Add(new ApiLatencySampleDto
                    {
                        Timestamp = sample.Timestamp,
                        AvgLatencyMs = avgLatency,
                        P95LatencyMs = p95Latency
                    });
                }
            }

            _logger.LogDebug("Retrieved {Count} API latency samples for last {Hours} hours", result.Count, hours);
            return result;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<ApiRequestVolumeDto> GetRequestVolume(int hours = 24)
    {
        lock (_lock)
        {
            UpdateHourlyBucket();

            var result = new List<ApiRequestVolumeDto>();
            var hoursToInclude = Math.Min(hours, 24);

            // Get request volume per hour
            for (int i = hoursToInclude - 1; i >= 0; i--)
            {
                var hourIndex = (_currentHourIndex - i + 24) % 24;
                var timestamp = DateTime.UtcNow.AddHours(-i);

                // Round to the start of the hour
                timestamp = new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, 0, 0, DateTimeKind.Utc);

                var requestCount = _hourlyRequests[hourIndex];

                if (requestCount > 0)
                {
                    result.Add(new ApiRequestVolumeDto
                    {
                        Timestamp = timestamp,
                        RequestCount = requestCount,
                        Category = "All" // Could be enhanced to track per-category volume
                    });
                }
            }

            _logger.LogDebug("Retrieved {Count} API request volume data points for last {Hours} hours", result.Count, hours);
            return result;
        }
    }

    /// <summary>
    /// Calculates the percentile value from a sorted list of doubles.
    /// </summary>
    /// <param name="sortedValues">A sorted list of double values.</param>
    /// <param name="percentile">The percentile to calculate (0-100).</param>
    /// <returns>The value at the specified percentile.</returns>
    private static double CalculatePercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        if (sortedValues.Count == 1)
        {
            return sortedValues[0];
        }

        // Calculate the index using the nearest-rank method
        var index = (int)Math.Ceiling((percentile / 100.0) * sortedValues.Count) - 1;

        // Clamp to valid range
        index = Math.Max(0, Math.Min(sortedValues.Count - 1, index));

        return sortedValues[index];
    }

    /// <summary>
    /// Internal class for tracking API category metrics.
    /// </summary>
    private class ApiCategory
    {
        public long RequestCount;
        public long TotalLatencyMs;
        public long ErrorCount;
    }

    /// <summary>
    /// Internal class for storing rate limit events.
    /// </summary>
    private class RateLimitEvent
    {
        public required DateTime Timestamp { get; init; }
        public required string Endpoint { get; init; }
        public required int RetryAfterMs { get; init; }
        public required bool IsGlobal { get; init; }
    }

    /// <summary>
    /// Internal struct for storing aggregated latency samples in the circular buffer.
    /// Each sample represents a 5-minute bucket of aggregated latency measurements.
    /// </summary>
    private struct LatencySample
    {
        public DateTime Timestamp { get; set; }
        public long TotalLatencyMs { get; set; }
        public int Count { get; set; }
        public int MinLatencyMs { get; set; }
        public int MaxLatencyMs { get; set; }
    }

    #region IMemoryReportable Implementation

    /// <inheritdoc/>
    public string ServiceName => "API Request Tracker";

    /// <inheritdoc/>
    public ServiceMemoryReportDto GetMemoryReport()
    {
        lock (_lock)
        {
            // Hourly buckets: 24 * 8 bytes = 192 bytes
            const int hourlyBucketsBytes = 24 * 8;

            // Latency samples: 288 samples * ~32 bytes (struct with DateTime + 2 longs + 2 ints)
            const int latencySampleBytes = 32;
            var latencyBufferBytes = _latencySamples.Length * latencySampleBytes;

            // Rate limit events: estimate ~100 bytes per event (DateTime + string + int + bool)
            const int rateLimitEventBytes = 100;
            var rateLimitQueueBytes = _rateLimitEventCount * rateLimitEventBytes;

            // Categories dictionary: estimate ~200 bytes per category (key + ApiCategory object)
            const int categoryBytes = 200;
            var categoriesBytes = _categories.Count * categoryBytes;

            var totalBytes = hourlyBucketsBytes + latencyBufferBytes + rateLimitQueueBytes + categoriesBytes;

            return new ServiceMemoryReportDto
            {
                ServiceName = ServiceName,
                EstimatedBytes = totalBytes,
                ItemCount = _categories.Count + _rateLimitEventCount + _latencySampleCount,
                Details = $"Categories: {_categories.Count}, Rate limits: {_rateLimitEventCount}/{MaxRateLimitEvents}, Latency samples: {_latencySampleCount}/288"
            };
        }
    }

    #endregion
}
