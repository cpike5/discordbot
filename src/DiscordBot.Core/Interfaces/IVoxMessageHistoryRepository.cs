using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for VoxMessageHistory entities with history-specific operations.
/// </summary>
public interface IVoxMessageHistoryRepository : IRepository<VoxMessageHistory>
{
    /// <summary>
    /// Gets the most recent VOX messages played by a user in a specific guild.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of recent history entries ordered by most recent first.</returns>
    Task<IReadOnlyList<VoxMessageHistory>> GetRecentAsync(
        ulong userId,
        ulong guildId,
        int limit = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all favorited VOX messages for a user in a specific guild.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of favorited history entries ordered by most recent first.</returns>
    Task<IReadOnlyList<VoxMessageHistory>> GetFavoritesAsync(
        ulong userId,
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the favorite status of a history entry.
    /// </summary>
    /// <param name="id">The history entry ID.</param>
    /// <param name="isFavorite">Whether to mark as favorite.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetFavoriteAsync(
        int id,
        bool isFavorite,
        CancellationToken cancellationToken = default);
}
