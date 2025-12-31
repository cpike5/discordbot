using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for ModTag entities with tag-specific operations.
/// </summary>
public interface IModTagRepository : IRepository<ModTag>
{
    /// <summary>
    /// Gets all mod tags defined for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of mod tags ordered by name.</returns>
    Task<IEnumerable<ModTag>> GetByGuildAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a mod tag by guild and name.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="name">Tag name (case-insensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The mod tag, or null if not found.</returns>
    Task<ModTag?> GetByNameAsync(
        ulong guildId,
        string name,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a tag with the specified name already exists in the guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="name">Tag name to check (case-insensitive).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a tag with the name exists, false otherwise.</returns>
    Task<bool> NameExistsAsync(
        ulong guildId,
        string name,
        CancellationToken cancellationToken = default);
}
