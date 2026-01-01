using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for GuildMetricsSnapshot entities.
/// </summary>
public interface IGuildMetricsRepository : IRepository<GuildMetricsSnapshot>
{
    /// <summary>
    /// Gets daily metrics for a guild within a date range.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="startDate">Start date (inclusive).</param>
    /// <param name="endDate">End date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of daily snapshots ordered by date.</returns>
    Task<IReadOnlyList<GuildMetricsSnapshot>> GetByDateRangeAsync(
        ulong guildId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent snapshot for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The most recent snapshot, or null if none exist.</returns>
    Task<GuildMetricsSnapshot?> GetLatestAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets member growth over time (joins - leaves).
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="startDate">Start date (inclusive).</param>
    /// <param name="endDate">End date (inclusive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Growth time series data as (Date, NetGrowth, Joined, Left) tuples.</returns>
    Task<IReadOnlyList<(DateOnly Date, int NetGrowth, int Joined, int Left)>> GetGrowthTimeSeriesAsync(
        ulong guildId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a daily snapshot (insert or update if exists).
    /// </summary>
    /// <param name="snapshot">The snapshot to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(
        GuildMetricsSnapshot snapshot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes snapshots older than the specified date.
    /// </summary>
    /// <param name="cutoff">Cutoff date - snapshots before this will be deleted.</param>
    /// <param name="batchSize">Maximum number of records to delete in this batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of snapshots deleted.</returns>
    Task<int> DeleteOlderThanAsync(
        DateOnly cutoff,
        int batchSize,
        CancellationToken cancellationToken = default);
}
