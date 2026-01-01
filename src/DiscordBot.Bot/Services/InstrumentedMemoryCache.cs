using System.Collections.Concurrent;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using DiscordBot.Core.Configuration;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Instrumented wrapper around IMemoryCache that tracks hit/miss statistics by key prefix.
/// Thread-safe singleton service using concurrent dictionaries for statistics tracking.
/// </summary>
public class InstrumentedMemoryCache : IInstrumentedCache
{
    private readonly IMemoryCache _innerCache;
    private readonly ILogger<InstrumentedMemoryCache> _logger;
    private readonly PerformanceMetricsOptions _options;

    private readonly ConcurrentDictionary<string, CacheStats> _statsByPrefix = new();

    public InstrumentedMemoryCache(
        IMemoryCache innerCache,
        ILogger<InstrumentedMemoryCache> logger,
        IOptions<PerformanceMetricsOptions> options)
    {
        _innerCache = innerCache;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc/>
    public bool TryGetValue<T>(object key, out T? value)
    {
        var result = _innerCache.TryGetValue(key, out value);

        if (_options.CacheStatisticsEnabled)
        {
            var prefix = ExtractKeyPrefix(key);
            var stats = _statsByPrefix.GetOrAdd(prefix, _ => new CacheStats());

            if (result)
            {
                Interlocked.Increment(ref stats.Hits);
                _logger.LogTrace("Cache hit: {Key} (Prefix: {Prefix})", key, prefix);
            }
            else
            {
                Interlocked.Increment(ref stats.Misses);
                _logger.LogTrace("Cache miss: {Key} (Prefix: {Prefix})", key, prefix);
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public void Set<T>(object key, T value, TimeSpan? absoluteExpiration = null)
    {
        var options = new MemoryCacheEntryOptions();

        if (absoluteExpiration.HasValue)
        {
            options.AbsoluteExpirationRelativeToNow = absoluteExpiration.Value;
        }

        _innerCache.Set(key, value, options);

        if (_options.CacheStatisticsEnabled)
        {
            var prefix = ExtractKeyPrefix(key);
            var stats = _statsByPrefix.GetOrAdd(prefix, _ => new CacheStats());
            Interlocked.Increment(ref stats.Size);

            _logger.LogTrace("Cache set: {Key} (Prefix: {Prefix})", key, prefix);
        }
    }

    /// <inheritdoc/>
    public void Remove(object key)
    {
        _innerCache.Remove(key);

        if (_options.CacheStatisticsEnabled)
        {
            var prefix = ExtractKeyPrefix(key);
            if (_statsByPrefix.TryGetValue(prefix, out var stats))
            {
                var currentSize = Interlocked.Read(ref stats.Size);
                if (currentSize > 0)
                {
                    Interlocked.Decrement(ref stats.Size);
                }
            }

            _logger.LogTrace("Cache remove: {Key} (Prefix: {Prefix})", key, prefix);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<CacheStatisticsDto> GetStatistics()
    {
        return _statsByPrefix.Select(kvp => CreateStatisticsDto(kvp.Key, kvp.Value)).ToList();
    }

    /// <inheritdoc/>
    public CacheStatisticsDto GetStatisticsByPrefix(string prefix)
    {
        if (_statsByPrefix.TryGetValue(prefix, out var stats))
        {
            return CreateStatisticsDto(prefix, stats);
        }

        return new CacheStatisticsDto
        {
            KeyPrefix = prefix,
            Hits = 0,
            Misses = 0,
            HitRate = 0,
            Size = 0
        };
    }

    /// <inheritdoc/>
    public void ResetStatistics()
    {
        _statsByPrefix.Clear();
        _logger.LogInformation("Cache statistics reset");
    }

    /// <summary>
    /// Extracts the key prefix from a cache key.
    /// Uses the first segment before ':' or '_' as the prefix.
    /// </summary>
    private static string ExtractKeyPrefix(object key)
    {
        var keyString = key.ToString() ?? "unknown";

        // Try splitting by ':' first (common pattern like "guilds:123")
        var colonIndex = keyString.IndexOf(':');
        if (colonIndex > 0)
        {
            return keyString[..colonIndex];
        }

        // Try splitting by '_' (pattern like "user_123")
        var underscoreIndex = keyString.IndexOf('_');
        if (underscoreIndex > 0)
        {
            return keyString[..underscoreIndex];
        }

        // No delimiter found, use the whole key as prefix
        return keyString;
    }

    /// <summary>
    /// Creates a CacheStatisticsDto from internal stats.
    /// </summary>
    private static CacheStatisticsDto CreateStatisticsDto(string prefix, CacheStats stats)
    {
        var hits = Interlocked.Read(ref stats.Hits);
        var misses = Interlocked.Read(ref stats.Misses);
        var total = hits + misses;
        var hitRate = total > 0 ? (double)hits / total * 100.0 : 0.0;

        return new CacheStatisticsDto
        {
            KeyPrefix = prefix,
            Hits = hits,
            Misses = misses,
            HitRate = hitRate,
            Size = (int)Interlocked.Read(ref stats.Size)
        };
    }

    /// <summary>
    /// Internal class for tracking cache statistics per prefix.
    /// </summary>
    private class CacheStats
    {
        public long Hits;
        public long Misses;
        public long Size;
    }
}
