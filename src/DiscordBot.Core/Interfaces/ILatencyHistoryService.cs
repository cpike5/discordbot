using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for storing and analyzing Discord gateway latency history.
/// </summary>
public interface ILatencyHistoryService
{
    /// <summary>
    /// Records a new latency sample from the Discord gateway.
    /// </summary>
    /// <param name="latencyMs">The latency value in milliseconds.</param>
    void RecordSample(int latencyMs);

    /// <summary>
    /// Gets the most recently recorded latency value.
    /// </summary>
    /// <returns>The current latency in milliseconds.</returns>
    int GetCurrentLatency();

    /// <summary>
    /// Gets latency samples for a specified number of hours.
    /// </summary>
    /// <param name="hours">The number of hours of history to retrieve (default: 24).</param>
    /// <returns>A read-only list of latency samples in chronological order.</returns>
    IReadOnlyList<LatencySampleDto> GetSamples(int hours = 24);

    /// <summary>
    /// Gets statistical analysis of latency samples over a specified number of hours.
    /// </summary>
    /// <param name="hours">The number of hours to analyze (default: 24).</param>
    /// <returns>Statistical summary including average, min, max, and percentiles.</returns>
    LatencyStatisticsDto GetStatistics(int hours = 24);
}
