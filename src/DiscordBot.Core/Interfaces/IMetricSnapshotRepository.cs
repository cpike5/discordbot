using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository for persisting and retrieving historical metric snapshots.
/// </summary>
public interface IMetricSnapshotRepository
{
    /// <summary>
    /// Adds a new metric snapshot.
    /// </summary>
    Task AddAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets snapshots within a time range, optionally aggregated.
    /// </summary>
    /// <param name="startTime">Start of the time range (UTC).</param>
    /// <param name="endTime">End of the time range (UTC).</param>
    /// <param name="aggregationMinutes">
    /// If > 0, aggregate samples into buckets of this size (in minutes).
    /// If 0, return raw samples.
    /// </param>
    Task<IReadOnlyList<MetricSnapshotDto>> GetRangeAsync(
        DateTime startTime,
        DateTime endTime,
        int aggregationMinutes = 0,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent snapshot.
    /// </summary>
    Task<MetricSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes snapshots older than the specified date.
    /// </summary>
    /// <returns>Number of deleted records.</returns>
    Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of snapshots in a time range.
    /// </summary>
    Task<int> GetCountAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default);
}
