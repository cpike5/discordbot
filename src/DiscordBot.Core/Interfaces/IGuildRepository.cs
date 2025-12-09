using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for Guild entities with Discord-specific operations.
/// </summary>
public interface IGuildRepository : IRepository<Guild>
{
    /// <summary>
    /// Gets a guild by its Discord snowflake ID.
    /// </summary>
    Task<Guild?> GetByDiscordIdAsync(ulong discordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active guilds.
    /// </summary>
    Task<IReadOnlyList<Guild>> GetActiveGuildsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a guild with its command logs.
    /// </summary>
    Task<Guild?> GetWithCommandLogsAsync(ulong discordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the guild's active status.
    /// </summary>
    Task SetActiveStatusAsync(ulong discordId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a guild record.
    /// </summary>
    Task<Guild> UpsertAsync(Guild guild, CancellationToken cancellationToken = default);
}
