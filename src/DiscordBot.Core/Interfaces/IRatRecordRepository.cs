using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for RatRecord entities with record-specific operations.
/// </summary>
public interface IRatRecordRepository : IRepository<RatRecord>
{
    /// <summary>
    /// Gets the total count of guilty verdicts for a user in a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total number of guilty verdicts for the user.</returns>
    Task<int> GetGuiltyCountAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent guilty records for a user in a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="limit">Maximum number of records to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of recent records ordered by RecordedAt descending.</returns>
    Task<IEnumerable<RatRecord>> GetRecentRecordsAsync(
        ulong guildId,
        ulong userId,
        int limit = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the leaderboard of users with the most guilty verdicts in a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of tuples containing user ID and their guilty count, ordered by count descending.</returns>
    Task<IEnumerable<(ulong UserId, int GuiltyCount)>> GetLeaderboardAsync(
        ulong guildId,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
