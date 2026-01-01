using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for ChannelActivitySnapshot entities.
/// </summary>
public interface IChannelActivityRepository : IRepository<ChannelActivitySnapshot>
{
    /// <summary>
    /// Gets channel activity rankings for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="startDate">Start of date range (inclusive).</param>
    /// <param name="endDate">End of date range (inclusive).</param>
    /// <param name="limit">Maximum number of channels to return (default 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of channels ordered by total message count descending.</returns>
    Task<IReadOnlyList<ChannelActivitySnapshot>> GetChannelRankingsAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets activity for a specific channel over time.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="channelId">Discord channel ID.</param>
    /// <param name="startDate">Start of date range (inclusive).</param>
    /// <param name="endDate">End of date range (inclusive).</param>
    /// <param name="granularity">Snapshot granularity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of channel activity snapshots ordered by period.</returns>
    Task<IReadOnlyList<ChannelActivitySnapshot>> GetChannelTimeSeriesAsync(
        ulong guildId,
        ulong channelId,
        DateTime startDate,
        DateTime endDate,
        SnapshotGranularity granularity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a snapshot (insert or update if exists).
    /// </summary>
    /// <param name="snapshot">The snapshot to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpsertAsync(
        ChannelActivitySnapshot snapshot,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes snapshots older than the specified date.
    /// </summary>
    /// <param name="cutoff">Cutoff date - snapshots before this will be deleted.</param>
    /// <param name="granularity">Granularity filter.</param>
    /// <param name="batchSize">Maximum number of records to delete in this batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of snapshots deleted.</returns>
    Task<int> DeleteOlderThanAsync(
        DateTime cutoff,
        SnapshotGranularity granularity,
        int batchSize,
        CancellationToken cancellationToken = default);
}
