using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for managing the watchlist of flagged users.
/// </summary>
public interface IWatchlistService
{
    /// <summary>
    /// Adds a user to the watchlist.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID to add.</param>
    /// <param name="reason">The optional reason for adding to watchlist.</param>
    /// <param name="addedById">The moderator adding the user.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created watchlist entry DTO.</returns>
    Task<WatchlistEntryDto> AddToWatchlistAsync(ulong guildId, ulong userId, string? reason, ulong addedById, CancellationToken ct = default);

    /// <summary>
    /// Removes a user from the watchlist.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the user was removed, false if not on watchlist.</returns>
    Task<bool> RemoveFromWatchlistAsync(ulong guildId, ulong userId, CancellationToken ct = default);

    /// <summary>
    /// Gets all watchlist entries for a guild with pagination.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the watchlist entries and total count.</returns>
    Task<(IEnumerable<WatchlistEntryDto> Items, int TotalCount)> GetWatchlistAsync(ulong guildId, int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>
    /// Checks if a user is on the watchlist.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the user is on the watchlist, false otherwise.</returns>
    Task<bool> IsOnWatchlistAsync(ulong guildId, ulong userId, CancellationToken ct = default);

    /// <summary>
    /// Gets a watchlist entry for a specific user.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The watchlist entry DTO, or null if not on watchlist.</returns>
    Task<WatchlistEntryDto?> GetEntryAsync(ulong guildId, ulong userId, CancellationToken ct = default);
}
