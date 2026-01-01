using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for an instrumented cache that tracks hit/miss statistics.
/// Wraps IMemoryCache to provide metrics by key prefix.
/// </summary>
public interface IInstrumentedCache
{
    /// <summary>
    /// Attempts to get a value from the cache.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The cached value if found, or default if not.</param>
    /// <returns>True if the key was found in the cache; otherwise, false.</returns>
    bool TryGetValue<T>(object key, out T? value);

    /// <summary>
    /// Sets a value in the cache with optional expiration.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="absoluteExpiration">Optional absolute expiration time from now.</param>
    void Set<T>(object key, T value, TimeSpan? absoluteExpiration = null);

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    void Remove(object key);

    /// <summary>
    /// Gets cache statistics for all tracked key prefixes.
    /// </summary>
    /// <returns>A read-only list of cache statistics by prefix.</returns>
    IReadOnlyList<CacheStatisticsDto> GetStatistics();

    /// <summary>
    /// Gets cache statistics for a specific key prefix.
    /// </summary>
    /// <param name="prefix">The key prefix to get statistics for.</param>
    /// <returns>Cache statistics for the specified prefix.</returns>
    CacheStatisticsDto GetStatisticsByPrefix(string prefix);

    /// <summary>
    /// Resets all cache statistics counters. Does not clear the cache itself.
    /// </summary>
    void ResetStatistics();
}
