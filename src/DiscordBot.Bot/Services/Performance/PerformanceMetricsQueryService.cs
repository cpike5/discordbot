using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Performance;

/// <summary>
/// Default implementation of <see cref="IPerformanceMetricsQueryService"/>. Holds the
/// aggregation/calculation logic that previously lived inline in
/// PerformanceMetricsController: time-range bucketing, historical-snapshot statistics,
/// command error-rate aggregation, and overall cache statistics.
/// </summary>
public class PerformanceMetricsQueryService : IPerformanceMetricsQueryService
{
    private readonly ICommandPerformanceAggregator _commandPerformanceAggregator;
    private readonly IMetricSnapshotRepository _metricSnapshotRepository;
    private readonly IInstrumentedCache _instrumentedCache;

    public PerformanceMetricsQueryService(
        ICommandPerformanceAggregator commandPerformanceAggregator,
        IMetricSnapshotRepository metricSnapshotRepository,
        IInstrumentedCache instrumentedCache)
    {
        _commandPerformanceAggregator = commandPerformanceAggregator;
        _metricSnapshotRepository = metricSnapshotRepository;
        _instrumentedCache = instrumentedCache;
    }

    /// <inheritdoc />
    public (int aggregationMinutes, string granularityLabel) GetAggregationForTimeRange(int hours)
    {
        return hours switch
        {
            <= 6 => (0, "raw"),              // 1-6 hours: raw samples
            <= 24 => (5, "5m"),              // 7-24 hours: 5-minute buckets
            <= 168 => (15, "15m"),           // 25-168 hours (7 days): 15-minute buckets
            _ => (60, "1h")                  // 169-720 hours (30 days): 1-hour buckets
        };
    }

    /// <inheritdoc />
    public async Task<HistoricalMetricsResponseDto> GetHistoricalMetricsAsync(int hours, string metric, CancellationToken cancellationToken)
    {
        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddHours(-hours);

        var (aggregationMinutes, granularityLabel) = GetAggregationForTimeRange(hours);

        var snapshots = await _metricSnapshotRepository.GetRangeAsync(
            startTime,
            endTime,
            aggregationMinutes,
            cancellationToken);

        return new HistoricalMetricsResponseDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Granularity = granularityLabel,
            Snapshots = snapshots
        };
    }

    /// <inheritdoc />
    public async Task<DatabaseHistoryResponseDto> GetDatabaseHistoryAsync(int hours, CancellationToken cancellationToken)
    {
        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddHours(-hours);

        var (aggregationMinutes, _) = GetAggregationForTimeRange(hours);

        var snapshots = await _metricSnapshotRepository.GetRangeAsync(
            startTime,
            endTime,
            aggregationMinutes,
            cancellationToken);

        var samples = snapshots.Select(s => new DatabaseHistorySampleDto
        {
            Timestamp = s.Timestamp,
            AvgQueryTimeMs = s.DatabaseAvgQueryTimeMs,
            TotalQueries = s.DatabaseTotalQueries,
            SlowQueryCount = s.DatabaseSlowQueryCount
        }).ToList();

        var statistics = new DatabaseHistoryStatisticsDto();
        if (samples.Count > 0)
        {
            statistics = new DatabaseHistoryStatisticsDto
            {
                AvgQueryTimeMs = samples.Average(s => s.AvgQueryTimeMs),
                MinQueryTimeMs = samples.Min(s => s.AvgQueryTimeMs),
                MaxQueryTimeMs = samples.Max(s => s.AvgQueryTimeMs),
                TotalSlowQueries = samples.Sum(s => s.SlowQueryCount)
            };
        }

        return new DatabaseHistoryResponseDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Samples = samples,
            Statistics = statistics
        };
    }

    /// <inheritdoc />
    public async Task<MemoryHistoryResponseDto> GetMemoryHistoryAsync(int hours, CancellationToken cancellationToken)
    {
        var endTime = DateTime.UtcNow;
        var startTime = endTime.AddHours(-hours);

        var (aggregationMinutes, _) = GetAggregationForTimeRange(hours);

        var snapshots = await _metricSnapshotRepository.GetRangeAsync(
            startTime,
            endTime,
            aggregationMinutes,
            cancellationToken);

        var samples = snapshots.Select(s => new MemoryHistorySampleDto
        {
            Timestamp = s.Timestamp,
            WorkingSetMB = s.WorkingSetMB,
            HeapSizeMB = s.HeapSizeMB,
            PrivateMemoryMB = s.PrivateMemoryMB
        }).ToList();

        var statistics = new MemoryHistoryStatisticsDto();
        if (samples.Count > 0)
        {
            statistics = new MemoryHistoryStatisticsDto
            {
                AvgWorkingSetMB = samples.Average(s => s.WorkingSetMB),
                MaxWorkingSetMB = samples.Max(s => s.WorkingSetMB),
                AvgHeapSizeMB = samples.Average(s => s.HeapSizeMB)
            };
        }

        return new MemoryHistoryResponseDto
        {
            StartTime = startTime,
            EndTime = endTime,
            Samples = samples,
            Statistics = statistics
        };
    }

    /// <inheritdoc />
    public async Task<CommandErrorsDto> GetCommandErrorsAsync(int hours, int limit)
    {
        var errorBreakdown = await _commandPerformanceAggregator.GetErrorBreakdownAsync(hours, limit);

        // Calculate overall error rate from aggregates
        var aggregates = await _commandPerformanceAggregator.GetAggregatesAsync(hours);
        var totalCommands = aggregates.Sum(a => a.ExecutionCount);
        var totalErrors = aggregates.Sum(a => (int)(a.ExecutionCount * (a.ErrorRate / 100.0)));
        var overallErrorRate = totalCommands > 0 ? (totalErrors * 100.0 / totalCommands) : 0;

        // Create recent errors list from error breakdown
        var recentErrors = errorBreakdown
            .SelectMany(eb => eb.ErrorMessages.Select(em => new RecentCommandErrorDto
            {
                Timestamp = DateTime.UtcNow, // Note: This is approximate, actual timestamps would need to come from command logs
                CommandName = eb.CommandName,
                ErrorMessage = em.Key,
                GuildId = null
            }))
            .Take(limit)
            .ToList();

        return new CommandErrorsDto
        {
            ErrorRate = overallErrorRate,
            ByType = errorBreakdown,
            RecentErrors = recentErrors
        };
    }

    /// <inheritdoc />
    public CacheSummaryDto GetCacheSummary()
    {
        var statisticsByPrefix = _instrumentedCache.GetStatistics();

        var totalHits = statisticsByPrefix.Sum(s => s.Hits);
        var totalMisses = statisticsByPrefix.Sum(s => s.Misses);
        var totalAccesses = totalHits + totalMisses;
        var overallHitRate = totalAccesses > 0 ? (totalHits * 100.0 / totalAccesses) : 0;
        var totalSize = statisticsByPrefix.Sum(s => s.Size);

        var overall = new CacheStatisticsDto
        {
            KeyPrefix = "Overall",
            Hits = totalHits,
            Misses = totalMisses,
            HitRate = overallHitRate,
            Size = totalSize
        };

        return new CacheSummaryDto
        {
            Overall = overall,
            ByType = statisticsByPrefix
        };
    }
}
