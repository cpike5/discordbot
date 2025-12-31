using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for UserModTag entities with tag assignment operations.
/// </summary>
public interface IUserModTagRepository : IRepository<UserModTag>
{
    /// <summary>
    /// Gets all tag assignments for a user including tag details.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="userId">Discord user ID to get tags for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of user mod tags with tag navigation property loaded.</returns>
    Task<IEnumerable<UserModTag>> GetByUserAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific tag is already assigned to a user.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="tagId">Tag definition ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the tag is assigned to the user, false otherwise.</returns>
    Task<bool> ExistsAsync(
        ulong guildId,
        ulong userId,
        Guid tagId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific tag assignment for removal.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="tagId">Tag definition ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user mod tag assignment, or null if not found.</returns>
    Task<UserModTag?> GetAssignmentAsync(
        ulong guildId,
        ulong userId,
        Guid tagId,
        CancellationToken cancellationToken = default);
}
