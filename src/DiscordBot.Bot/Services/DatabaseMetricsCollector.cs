using System.Collections.Concurrent;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;
using DiscordBot.Core.Configuration;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for collecting database query performance metrics.
/// Integrates with QueryPerformanceInterceptor to track query execution times and slow queries.
/// Thread-safe singleton service using concurrent collections.
/// </summary>
public class DatabaseMetricsCollector : IDatabaseMetricsCollector
{
    private readonly ILogger<DatabaseMetricsCollector> _logger;
    private readonly PerformanceMetricsOptions _options;
    private readonly object _lock = new();

    // Metrics
    private long _totalQueries;
    private long _totalDurationMs;
    private long _errorCount;

    // Query duration histogram buckets (in milliseconds)
    private readonly int[] _histogram = new int[5]; // <10ms, 10-50ms, 50-100ms, 100-500ms, >500ms

    // Slow query tracking
    private readonly ConcurrentQueue<SlowQuery> _slowQueries = new();
    private int _slowQueryCount;

    public DatabaseMetricsCollector(
        ILogger<DatabaseMetricsCollector> logger,
        IOptions<PerformanceMetricsOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public void RecordQuery(double durationMs, string commandType)
    {
        lock (_lock)
        {
            _totalQueries++;
            _totalDurationMs += (long)durationMs;

            // Update histogram
            if (durationMs < 10)
                _histogram[0]++;
            else if (durationMs < 50)
                _histogram[1]++;
            else if (durationMs < 100)
                _histogram[2]++;
            else if (durationMs < 500)
                _histogram[3]++;
            else
                _histogram[4]++;

            _logger.LogTrace("Recorded query: Type={CommandType}, Duration={DurationMs}ms", commandType, durationMs);
        }
    }

    /// <inheritdoc/>
    public void RecordQueryError(double durationMs, string error)
    {
        lock (_lock)
        {
            _errorCount++;
            _logger.LogDebug("Recorded query error: Duration={DurationMs}ms, Error={Error}", durationMs, error);
        }
    }

    /// <inheritdoc/>
    public void RecordSlowQuery(string commandText, double durationMs, string? parameters)
    {
        var slowQuery = new SlowQuery
        {
            Timestamp = DateTime.UtcNow,
            CommandText = commandText,
            DurationMs = durationMs,
            Parameters = parameters
        };

        _slowQueries.Enqueue(slowQuery);
        Interlocked.Increment(ref _slowQueryCount);

        // Trim to max stored
        while (_slowQueryCount > _options.SlowQueryMaxStored)
        {
            if (_slowQueries.TryDequeue(out _))
            {
                Interlocked.Decrement(ref _slowQueryCount);
            }
        }

        _logger.LogWarning(
            "Slow query detected: Duration={DurationMs}ms, Command={Command}",
            durationMs,
            commandText.Length > 100 ? commandText[..100] + "..." : commandText);
    }

    /// <inheritdoc/>
    public DatabaseMetricsDto GetMetrics()
    {
        lock (_lock)
        {
            var avgQueryTime = _totalQueries > 0 ? (double)_totalDurationMs / _totalQueries : 0;

            var histogram = new Dictionary<string, int>
            {
                ["0-10ms"] = _histogram[0],
                ["10-50ms"] = _histogram[1],
                ["50-100ms"] = _histogram[2],
                ["100-500ms"] = _histogram[3],
                [">500ms"] = _histogram[4]
            };

            return new DatabaseMetricsDto
            {
                TotalQueries = _totalQueries,
                AvgQueryTimeMs = avgQueryTime,
                SlowQueryCount = _slowQueryCount,
                QueryHistogram = histogram,
                ConnectionPoolStats = null // Could be populated from DbContext if needed
            };
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<SlowQueryDto> GetSlowQueries(int limit = 20)
    {
        // Take the most recent slow queries up to the limit
        return _slowQueries
            .Reverse()
            .Take(limit)
            .Select(sq => new SlowQueryDto
            {
                Timestamp = sq.Timestamp,
                CommandText = sq.CommandText,
                DurationMs = sq.DurationMs,
                Parameters = sq.Parameters
            })
            .ToList();
    }

    /// <inheritdoc/>
    public void Reset()
    {
        lock (_lock)
        {
            _totalQueries = 0;
            _totalDurationMs = 0;
            _errorCount = 0;
            Array.Clear(_histogram, 0, _histogram.Length);

            while (_slowQueries.TryDequeue(out _)) { }
            _slowQueryCount = 0;

            _logger.LogInformation("Database metrics reset");
        }
    }

    /// <summary>
    /// Internal class for storing slow query information.
    /// </summary>
    private class SlowQuery
    {
        public required DateTime Timestamp { get; init; }
        public required string CommandText { get; init; }
        public required double DurationMs { get; init; }
        public string? Parameters { get; init; }
    }
}
