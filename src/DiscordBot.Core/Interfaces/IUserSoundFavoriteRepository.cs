using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for UserSoundFavorite entities with favorite-specific operations.
/// </summary>
public interface IUserSoundFavoriteRepository : IRepository<UserSoundFavorite>
{
    /// <summary>
    /// Gets the IDs of all sounds a user has favorited in a specific guild.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of favorited sound IDs.</returns>
    Task<IReadOnlyList<Guid>> GetFavoriteSoundIdsAsync(
        ulong userId,
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific favorite record for a user, sound, and guild combination.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="soundId">Sound unique identifier.</param>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The favorite record if found, null otherwise.</returns>
    Task<UserSoundFavorite?> GetFavoriteAsync(
        ulong userId,
        Guid soundId,
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific favorite for a user, sound, and guild combination.
    /// No-op if the favorite does not exist.
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="soundId">Sound unique identifier.</param>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveFavoriteAsync(
        ulong userId,
        Guid soundId,
        ulong guildId,
        CancellationToken cancellationToken = default);
}
