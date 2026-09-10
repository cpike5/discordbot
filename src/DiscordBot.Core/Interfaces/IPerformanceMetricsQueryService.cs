using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Aggregation and calculation logic for performance metrics endpoints: bucket-size
/// selection for time ranges, statistics computed over historical snapshots, command
/// error-rate aggregation, and overall cache statistics. Extracted from
/// PerformanceMetricsController so the controller stays a thin set of endpoints.
/// </summary>
public interface IPerformanceMetricsQueryService
{
    /// <summary>
    /// Gets historical system metrics for charting, aggregated into buckets sized
    /// appropriately for the requested time range.
    /// </summary>
    Task<HistoricalMetricsResponseDto> GetHistoricalMetricsAsync(int hours, string metric, CancellationToken cancellationToken);

    /// <summary>
    /// Gets historical database performance metrics with computed statistics.
    /// </summary>
    Task<DatabaseHistoryResponseDto> GetDatabaseHistoryAsync(int hours, CancellationToken cancellationToken);

    /// <summary>
    /// Gets historical memory metrics with computed statistics.
    /// </summary>
    Task<MemoryHistoryResponseDto> GetMemoryHistoryAsync(int hours, CancellationToken cancellationToken);

    /// <summary>
    /// Gets command error breakdown along with the computed overall error rate.
    /// </summary>
    Task<CommandErrorsDto> GetCommandErrorsAsync(int hours, int limit);

    /// <summary>
    /// Gets cache statistics with the computed overall (all-prefix) summary.
    /// </summary>
    CacheSummaryDto GetCacheSummary();

    /// <summary>
    /// Determines the aggregation bucket size and granularity label for a requested time range.
    /// </summary>
    (int aggregationMinutes, string granularityLabel) GetAggregationForTimeRange(int hours);
}
