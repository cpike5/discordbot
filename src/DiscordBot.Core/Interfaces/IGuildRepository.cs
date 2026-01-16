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
    /// Updates the guild's active status and LeftAt timestamp.
    /// When isActive is false, sets LeftAt to the current UTC time.
    /// When isActive is true, clears LeftAt to null.
    /// </summary>
    Task SetActiveStatusAsync(ulong discordId, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a guild record.
    /// </summary>
    Task<Guild> UpsertAsync(Guild guild, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of guilds that joined since a specified date.
    /// </summary>
    /// <param name="since">Start date for counting guild joins.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of guilds joined since the specified date.</returns>
    Task<int> GetJoinedCountAsync(DateTime since, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of guilds that left (became inactive) since a specified date.
    /// </summary>
    /// <param name="since">Start date for counting guild leaves.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Count of guilds that left since the specified date.</returns>
    Task<int> GetLeftCountAsync(DateTime since, CancellationToken cancellationToken = default);
}
