namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for managing temporary state data for Discord component interactions.
/// </summary>
public interface IInteractionStateService
{
    /// <summary>
    /// Creates a new state entry and returns a correlation ID.
    /// </summary>
    /// <typeparam name="T">The type of state data</typeparam>
    /// <param name="userId">The user ID associated with this state</param>
    /// <param name="data">The state data to store</param>
    /// <param name="expiry">Optional custom expiry duration (defaults to 15 minutes)</param>
    /// <returns>An 8-character correlation ID for state lookup</returns>
    string CreateState<T>(ulong userId, T data, TimeSpan? expiry = null);

    /// <summary>
    /// Attempts to retrieve state data by correlation ID.
    /// </summary>
    /// <typeparam name="T">The expected type of state data</typeparam>
    /// <param name="correlationId">The correlation ID to lookup</param>
    /// <param name="state">The retrieved state data, or default if not found</param>
    /// <returns>True if state was found and is valid, false otherwise</returns>
    bool TryGetState<T>(string correlationId, out T? state);

    /// <summary>
    /// Attempts to remove state data by correlation ID.
    /// </summary>
    /// <param name="correlationId">The correlation ID to remove</param>
    /// <returns>True if state was found and removed, false otherwise</returns>
    bool TryRemoveState(string correlationId);

    /// <summary>
    /// Removes all expired state entries.
    /// </summary>
    /// <returns>The number of expired entries removed</returns>
    int CleanupExpired();

    /// <summary>
    /// Gets the number of active (non-expired) state entries.
    /// </summary>
    int ActiveStateCount { get; }
}
