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
public class ApiRequestTracker : IApiRequestTracker
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
                RecordRequest("REST", severity);
                _logger.LogTrace("Tracked REST API request: {Message}", message);
            }
            else if (source.Contains("Gateway", StringComparison.OrdinalIgnoreCase) ||
                     message.Contains("Gateway", StringComparison.OrdinalIgnoreCase))
            {
                RecordRequest("Gateway", severity);
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

    /// <summary>
    /// Records an API request for a specific category.
    /// </summary>
    private void RecordRequest(string category, int severity)
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
}
