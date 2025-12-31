using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for ModNote entities with note-specific operations.
/// </summary>
public interface IModNoteRepository : IRepository<ModNote>
{
    /// <summary>
    /// Gets mod notes for a specific user.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="targetUserId">Discord user ID to get notes for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of mod notes ordered by creation date descending.</returns>
    Task<IEnumerable<ModNote>> GetByUserAsync(
        ulong guildId,
        ulong targetUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets mod notes created by a specific moderator.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="authorUserId">Discord user ID of the note author.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of mod notes ordered by creation date descending.</returns>
    Task<IEnumerable<ModNote>> GetByAuthorAsync(
        ulong guildId,
        ulong authorUserId,
        CancellationToken cancellationToken = default);
}
