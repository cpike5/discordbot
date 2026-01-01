using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for MemberActivitySnapshot entities.
/// </summary>
public interface IMemberActivityRepository : IRepository<MemberActivitySnapshot>
{
    /// <summary>
    /// Gets activity snapshots for a specific member in a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="startDate">Start of date range (inclusive).</param>
    /// <param name="endDate">End of date range (inclusive).</param>
    /// <param name="granularity">Optional granularity filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of member activity snapshots ordered by period.</returns>
    Task<IReadOnlyList<MemberActivitySnapshot>> GetByMemberAsync(
        ulong guildId,
        ulong userId,
        DateTime startDate,
        DateTime endDate,
        SnapshotGranularity? granularity = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets top active members for a guild within a date range.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="startDate">Start of date range (inclusive).</param>
    /// <param name="endDate">End of date range (inclusive).</param>
    /// <param name="limit">Maximum number of members to return (default 10).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of top active members ordered by total message count descending.</returns>
    Task<IReadOnlyList<MemberActivitySnapshot>> GetTopActiveMembersAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets aggregated activity time series for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="startDate">Start of date range (inclusive).</param>
    /// <param name="endDate">End of date range (inclusive).</param>
    /// <param name="granularity">Snapshot granularity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Time series data as (Period, TotalMessages, ActiveMembers) tuples.</returns>
    Task<IReadOnlyList<(DateTime Period, int TotalMessages, int ActiveMembers)>> GetActivityTimeSeriesAsync(
        ulong guildId,
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
        MemberActivitySnapshot snapshot,
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

    /// <summary>
    /// Gets the most recent snapshot timestamp for a granularity.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="granularity">Snapshot granularity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The most recent period start time, or null if no snapshots exist.</returns>
    Task<DateTime?> GetLastSnapshotTimeAsync(
        ulong guildId,
        SnapshotGranularity granularity,
        CancellationToken cancellationToken = default);
}
