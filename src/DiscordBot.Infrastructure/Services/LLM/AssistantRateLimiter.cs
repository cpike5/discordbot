using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// <see cref="IAssistantRateLimiter"/> backed by <see cref="IMemoryCache"/>. Extracted from the
/// guild assistant service so the guild and DM assistants share one fixed-window rate-limiting
/// implementation, namespaced by cache-key prefix so their windows can never collide.
/// </summary>
public class AssistantRateLimiter : IAssistantRateLimiter
{
    private readonly IMemoryCache _cache;
    private readonly object _gate = new();

    public AssistantRateLimiter(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public Task<RateLimitCheckResult> CheckAsync(
        string cacheKeyPrefix,
        string scopeKey,
        int limit,
        int windowMinutes,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(cacheKeyPrefix, scopeKey);

        lock (_gate)
        {
            var usageEntry = CurrentEntry(cacheKey, windowMinutes);
            var count = usageEntry?.Count ?? 0;

            return Task.FromResult(count >= limit
                ? Limited(usageEntry!, limit, windowMinutes)
                : RateLimitCheckResult.Allowed(limit - count));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// The check and the increment happen under one lock. The entry is a mutable object shared
    /// through the cache, so without it two requests could both read <c>Count</c> below the limit
    /// and both proceed. The lock is per limiter instance (a singleton) rather than per key: the
    /// critical section is a cache read and an integer bump, never I/O.
    /// </remarks>
    public Task<RateLimitCheckResult> TryReserveAsync(
        string cacheKeyPrefix,
        string scopeKey,
        int limit,
        int windowMinutes,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey(cacheKeyPrefix, scopeKey);

        lock (_gate)
        {
            var entry = CurrentEntry(cacheKey, windowMinutes);
            if (entry != null && entry.Count >= limit)
            {
                return Task.FromResult(Limited(entry, limit, windowMinutes));
            }

            if (entry == null)
            {
                entry = new RateLimitUsageEntry { WindowStart = DateTime.UtcNow };
                var expiry = entry.WindowStart.AddMinutes(windowMinutes);
                _cache.Set(cacheKey, entry, new MemoryCacheEntryOptions().SetAbsoluteExpiration(expiry));
            }

            entry.Count++;
            return Task.FromResult(RateLimitCheckResult.Allowed(limit - entry.Count));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Decrements whatever window is current. A release that lands after the reserving window
    /// expired finds no entry and does nothing; one that lands after a new window has started
    /// refunds a slot in the new window, a one-off over-refund that is accepted rather than tracking
    /// a window id per reservation.
    /// </remarks>
    public void Release(string cacheKeyPrefix, string scopeKey)
    {
        var cacheKey = BuildCacheKey(cacheKeyPrefix, scopeKey);

        lock (_gate)
        {
            var entry = _cache.Get<RateLimitUsageEntry>(cacheKey);
            if (entry is { Count: > 0 })
            {
                entry.Count--;
            }
        }
    }

    /// <summary>
    /// The entry for the current window, or null when there is none or it has expired (in which
    /// case it is removed). Call under <see cref="_gate"/>.
    /// </summary>
    private RateLimitUsageEntry? CurrentEntry(string cacheKey, int windowMinutes)
    {
        var entry = _cache.Get<RateLimitUsageEntry>(cacheKey);
        if (entry != null && DateTime.UtcNow >= entry.WindowStart.AddMinutes(windowMinutes))
        {
            _cache.Remove(cacheKey);
            return null;
        }

        return entry;
    }

    private static RateLimitCheckResult Limited(RateLimitUsageEntry entry, int limit, int windowMinutes)
    {
        var retryAfter = entry.WindowStart.AddMinutes(windowMinutes) - DateTime.UtcNow;
        var minutes = (int)Math.Ceiling(retryAfter.TotalMinutes);

        return RateLimitCheckResult.RateLimited(
            retryAfter,
            $"You've reached your question limit ({limit} per {windowMinutes} minutes). Try again in {minutes} minute(s).");
    }

    private static string BuildCacheKey(string cacheKeyPrefix, string scopeKey) => $"{cacheKeyPrefix}{scopeKey}";

    private class RateLimitUsageEntry
    {
        public DateTime WindowStart { get; set; }
        public int Count { get; set; }
    }
}
