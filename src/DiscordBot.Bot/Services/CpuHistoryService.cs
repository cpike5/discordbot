using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;
using DiscordBot.Core.Configuration;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for storing CPU usage history using a circular buffer.
/// Maintains 24 hours of samples at 5-second intervals (17,280 samples) by default.
/// Thread-safe singleton service with percentile calculation support.
/// </summary>
public class CpuHistoryService : ICpuHistoryService, IMemoryReportable
{
    private readonly ILogger<CpuHistoryService> _logger;
    private readonly PerformanceMetricsOptions _options;
    private readonly object _lock = new();

    private readonly CpuSample[] _samples;
    private readonly int _maxSamples;
    private int _currentIndex;
    private int _sampleCount;
    private double _currentCpu;

    public CpuHistoryService(
        ILogger<CpuHistoryService> logger,
        IOptions<PerformanceMetricsOptions> options)
    {
        _logger = logger;
        _options = options.Value;

        // Calculate max samples: (retention hours * 3600 seconds) / sample interval
        _maxSamples = (_options.CpuRetentionHours * 3600) / _options.CpuSampleIntervalSeconds;
        _samples = new CpuSample[_maxSamples];
        _currentIndex = 0;
        _sampleCount = 0;
        _currentCpu = 0;

        _logger.LogInformation(
            "CpuHistoryService initialized with capacity for {MaxSamples} samples ({Hours}h at {Interval}s intervals)",
            _maxSamples, _options.CpuRetentionHours, _options.CpuSampleIntervalSeconds);
    }

    /// <inheritdoc/>
    public void RecordSample(double cpuPercent)
    {
        lock (_lock)
        {
            var sample = new CpuSample
            {
                Timestamp = DateTime.UtcNow,
                CpuPercent = cpuPercent
            };

            _samples[_currentIndex] = sample;
            _currentIndex = (_currentIndex + 1) % _maxSamples;

            if (_sampleCount < _maxSamples)
            {
                _sampleCount++;
            }

            _currentCpu = cpuPercent;

            _logger.LogTrace("Recorded CPU sample: {CpuPercent:F1}% at {Timestamp}", cpuPercent, sample.Timestamp);
        }
    }

    /// <inheritdoc/>
    public double GetCurrentCpu()
    {
        lock (_lock)
        {
            return _currentCpu;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<CpuSampleDto> GetSamples(int hours = 24)
    {
        lock (_lock)
        {
            if (_sampleCount == 0)
            {
                return Array.Empty<CpuSampleDto>();
            }

            var cutoff = DateTime.UtcNow.AddHours(-hours);
            var result = new List<CpuSampleDto>();

            // Read samples in chronological order
            var startIndex = _sampleCount < _maxSamples ? 0 : _currentIndex;
            for (int i = 0; i < _sampleCount; i++)
            {
                var index = (startIndex + i) % _maxSamples;
                var sample = _samples[index];

                if (sample.Timestamp >= cutoff)
                {
                    result.Add(new CpuSampleDto
                    {
                        Timestamp = sample.Timestamp,
                        CpuPercent = sample.CpuPercent
                    });
                }
            }

            _logger.LogDebug("Retrieved {Count} CPU samples for last {Hours} hours", result.Count, hours);
            return result;
        }
    }

    /// <inheritdoc/>
    public CpuStatisticsDto GetStatistics(int hours = 24)
    {
        lock (_lock)
        {
            if (_sampleCount == 0)
            {
                return new CpuStatisticsDto
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
            var values = new List<double>();

            // Collect all CPU values within the time window
            var startIndex = _sampleCount < _maxSamples ? 0 : _currentIndex;
            for (int i = 0; i < _sampleCount; i++)
            {
                var index = (startIndex + i) % _maxSamples;
                var sample = _samples[index];

                if (sample.Timestamp >= cutoff)
                {
                    values.Add(sample.CpuPercent);
                }
            }

            if (values.Count == 0)
            {
                return new CpuStatisticsDto
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

            var statistics = new CpuStatisticsDto
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
                "Calculated CPU statistics for {Hours}h: Avg={Avg:F1}%, P50={P50:F1}%, P95={P95:F1}%, P99={P99:F1}%, Samples={Count}",
                hours, statistics.Average, statistics.P50, statistics.P95, statistics.P99, statistics.SampleCount);

            return statistics;
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
    /// Internal struct for storing CPU samples in the circular buffer.
    /// </summary>
    private struct CpuSample
    {
        public DateTime Timestamp { get; init; }
        public double CpuPercent { get; init; }
    }

    #region IMemoryReportable Implementation

    /// <inheritdoc/>
    public string ServiceName => "CPU History";

    /// <inheritdoc/>
    public ServiceMemoryReportDto GetMemoryReport()
    {
        lock (_lock)
        {
            // CpuSample struct: DateTime (8 bytes) + double (8 bytes) = 16 bytes
            const int sampleSizeBytes = 16;
            var estimatedBytes = _maxSamples * sampleSizeBytes;

            return new ServiceMemoryReportDto
            {
                ServiceName = ServiceName,
                EstimatedBytes = estimatedBytes,
                ItemCount = _sampleCount,
                Details = $"Circular buffer: {_sampleCount}/{_maxSamples} samples ({_options.CpuRetentionHours}h retention)"
            };
        }
    }

    #endregion
}
