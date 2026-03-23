using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for SoundCategory entities with category-specific operations.
/// </summary>
public interface ISoundCategoryRepository : IRepository<SoundCategory>
{
    /// <summary>
    /// Gets all categories for a specific guild, ordered by SortOrder then Name.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Read-only list of categories for the guild.</returns>
    Task<IReadOnlyList<SoundCategory>> GetByGuildAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reorders categories within a guild by updating their SortOrder values.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ordering">Collection of (Id, SortOrder) tuples specifying the new order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReorderAsync(
        ulong guildId,
        IEnumerable<(int Id, int SortOrder)> ordering,
        CancellationToken cancellationToken = default);
}
