using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for Watchlist entities with watchlist-specific operations.
/// </summary>
public interface IWatchlistRepository : IRepository<Watchlist>
{
    /// <summary>
    /// Gets all watchlist entries for a guild with pagination.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated watchlist entries and the total count.</returns>
    Task<(IEnumerable<Watchlist> Items, int TotalCount)> GetByGuildAsync(
        ulong guildId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user is on the watchlist for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="userId">Discord user ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user is on the watchlist, false otherwise.</returns>
    Task<bool> IsOnWatchlistAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a watchlist entry for a specific user in a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The watchlist entry, or null if not found.</returns>
    Task<Watchlist?> GetByUserAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default);
}
