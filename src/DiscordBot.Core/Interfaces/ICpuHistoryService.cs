using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for storing and analyzing CPU usage history.
/// </summary>
public interface ICpuHistoryService
{
    /// <summary>
    /// Records a new CPU usage sample.
    /// </summary>
    /// <param name="cpuPercent">The CPU usage percentage (0-100).</param>
    void RecordSample(double cpuPercent);

    /// <summary>
    /// Gets the most recently recorded CPU usage value.
    /// </summary>
    /// <returns>The current CPU usage percentage.</returns>
    double GetCurrentCpu();

    /// <summary>
    /// Gets CPU usage samples for a specified number of hours.
    /// </summary>
    /// <param name="hours">The number of hours of history to retrieve (default: 24).</param>
    /// <returns>A read-only list of CPU samples in chronological order.</returns>
    IReadOnlyList<CpuSampleDto> GetSamples(int hours = 24);

    /// <summary>
    /// Gets statistical analysis of CPU usage samples over a specified number of hours.
    /// </summary>
    /// <param name="hours">The number of hours to analyze (default: 24).</param>
    /// <returns>Statistical summary including average, min, max, and percentiles.</returns>
    CpuStatisticsDto GetStatistics(int hours = 24);
}
