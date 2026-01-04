using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;
using DiscordBot.Core.Configuration;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for storing Discord gateway latency history using a circular buffer.
/// Maintains 24 hours of samples at 30-second intervals (2,880 samples) by default.
/// Thread-safe singleton service with percentile calculation support.
/// </summary>
public class LatencyHistoryService : ILatencyHistoryService, IMemoryReportable
{
    private readonly ILogger<LatencyHistoryService> _logger;
    private readonly PerformanceMetricsOptions _options;
    private readonly object _lock = new();

    private readonly LatencySample[] _samples;
    private readonly int _maxSamples;
    private int _currentIndex;
    private int _sampleCount;
    private int _currentLatency;

    public LatencyHistoryService(
        ILogger<LatencyHistoryService> logger,
        IOptions<PerformanceMetricsOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Calculate max samples: (retention hours * 3600 seconds) / sample interval
        _maxSamples = (_options.LatencyRetentionHours * 3600) / _options.LatencySampleIntervalSeconds;
        _samples = new LatencySample[_maxSamples];
        _currentIndex = 0;
        _sampleCount = 0;
        _currentLatency = 0;

        _logger.LogInformation(
            "LatencyHistoryService initialized with capacity for {MaxSamples} samples ({Hours}h at {Interval}s intervals)",
            _maxSamples, _options.LatencyRetentionHours, _options.LatencySampleIntervalSeconds);
    }

    /// <inheritdoc/>
    public void RecordSample(int latencyMs)
    {
        lock (_lock)
        {
            var sample = new LatencySample
            {
                Timestamp = DateTime.UtcNow,
                LatencyMs = latencyMs
            };

            _samples[_currentIndex] = sample;
            _currentIndex = (_currentIndex + 1) % _maxSamples;

            if (_sampleCount < _maxSamples)
            {
                _sampleCount++;
            }

            _currentLatency = latencyMs;

            _logger.LogTrace("Recorded latency sample: {LatencyMs}ms at {Timestamp}", latencyMs, sample.Timestamp);
        }
    }

    /// <inheritdoc/>
    public int GetCurrentLatency()
    {
        lock (_lock)
        {
            return _currentLatency;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<LatencySampleDto> GetSamples(int hours = 24)
    {
        lock (_lock)
        {
            if (_sampleCount == 0)
            {
                return Array.Empty<LatencySampleDto>();
            }

            var cutoff = DateTime.UtcNow.AddHours(-hours);
            var result = new List<LatencySampleDto>();

            // Read samples in chronological order
            var startIndex = _sampleCount < _maxSamples ? 0 : _currentIndex;
            for (int i = 0; i < _sampleCount; i++)
            {
                var index = (startIndex + i) % _maxSamples;
                var sample = _samples[index];

                if (sample.Timestamp >= cutoff)
                {
                    result.Add(new LatencySampleDto
                    {
                        Timestamp = sample.Timestamp,
                        LatencyMs = sample.LatencyMs
                    });
                }
            }

            _logger.LogDebug("Retrieved {Count} latency samples for last {Hours} hours", result.Count, hours);
            return result;
        }
    }

    /// <inheritdoc/>
    public LatencyStatisticsDto GetStatistics(int hours = 24)
    {
        lock (_lock)
        {
            if (_sampleCount == 0)
            {
                return new LatencyStatisticsDto
                {
                    Average = 0,
                    Min = 0,
                    Max = 0,
                    P50 = 0,
                    P95 = 0,
                    P99 = 0,
                    SampleCount = 0
                };
            }

            var cutoff = DateTime.UtcNow.AddHours(-hours);
            var values = new List<int>();

            // Collect all latency values within the time window
            var startIndex = _sampleCount < _maxSamples ? 0 : _currentIndex;
            for (int i = 0; i < _sampleCount; i++)
            {
                var index = (startIndex + i) % _maxSamples;
                var sample = _samples[index];

                if (sample.Timestamp >= cutoff)
                {
                    values.Add(sample.LatencyMs);
                }
            }

            if (values.Count == 0)
            {
                return new LatencyStatisticsDto
                {
                    Average = 0,
                    Min = 0,
                    Max = 0,
                    P50 = 0,
                    P95 = 0,
                    P99 = 0,
                    SampleCount = 0
                };
            }

            // Sort for percentile calculation
            values.Sort();

            var statistics = new LatencyStatisticsDto
            {
                Average = values.Average(),
                Min = values.Min(),
                Max = values.Max(),
                P50 = CalculatePercentile(values, 50),
                P95 = CalculatePercentile(values, 95),
                P99 = CalculatePercentile(values, 99),
                SampleCount = values.Count
            };

            _logger.LogDebug(
                "Calculated latency statistics for {Hours}h: Avg={Avg:F1}ms, P50={P50}ms, P95={P95}ms, P99={P99}ms, Samples={Count}",
                hours, statistics.Average, statistics.P50, statistics.P95, statistics.P99, statistics.SampleCount);

            return statistics;
        }
    }

    /// <summary>
    /// Calculates the percentile value from a sorted list of integers.
    /// </summary>
    /// <param name="sortedValues">A sorted list of integer values.</param>
    /// <param name="percentile">The percentile to calculate (0-100).</param>
    /// <returns>The value at the specified percentile.</returns>
    private static int CalculatePercentile(List<int> sortedValues, double percentile)
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
    /// Internal class for storing latency samples in the circular buffer.
    /// </summary>
    private struct LatencySample
    {
        public DateTime Timestamp { get; init; }
        public int LatencyMs { get; init; }
    }

    #region IMemoryReportable Implementation

    /// <inheritdoc/>
    public string ServiceName => "Latency History";

    /// <inheritdoc/>
    public ServiceMemoryReportDto GetMemoryReport()
    {
        lock (_lock)
        {
            // LatencySample struct: DateTime (8 bytes) + int (4 bytes) = 12 bytes, aligned to 16 bytes
            const int sampleSizeBytes = 16;
            var estimatedBytes = _maxSamples * sampleSizeBytes;

            return new ServiceMemoryReportDto
            {
                ServiceName = ServiceName,
                EstimatedBytes = estimatedBytes,
                ItemCount = _sampleCount,
                Details = $"Circular buffer: {_sampleCount}/{_maxSamples} samples ({_options.LatencyRetentionHours}h retention)"
            };
        }
    }

    #endregion
}
