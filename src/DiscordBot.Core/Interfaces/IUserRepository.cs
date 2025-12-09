using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for User entities with Discord-specific operations.
/// </summary>
public interface IUserRepository : IRepository<User>
{
    /// <summary>
    /// Gets a user by their Discord snowflake ID.
    /// </summary>
    Task<User?> GetByDiscordIdAsync(ulong discordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a user with their command history.
    /// </summary>
    Task<User?> GetWithCommandLogsAsync(ulong discordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the user's last seen timestamp.
    /// </summary>
    Task UpdateLastSeenAsync(ulong discordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a user record.
    /// </summary>
    Task<User> UpsertAsync(User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets users who have been active within the specified timeframe.
    /// </summary>
    Task<IReadOnlyList<User>> GetRecentlyActiveAsync(
        TimeSpan timeframe,
        CancellationToken cancellationToken = default);
}
