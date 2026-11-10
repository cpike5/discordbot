using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces.LLM;
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
        var usageEntry = _cache.Get<RateLimitUsageEntry>(cacheKey);

        if (usageEntry == null)
        {
            return Task.FromResult(RateLimitCheckResult.Allowed(limit));
        }

        var windowExpiry = usageEntry.WindowStart.AddMinutes(windowMinutes);
        if (DateTime.UtcNow >= windowExpiry)
        {
            _cache.Remove(cacheKey);
            return Task.FromResult(RateLimitCheckResult.Allowed(limit));
        }

        if (usageEntry.Count >= limit)
        {
            var retryAfter = windowExpiry - DateTime.UtcNow;
            var minutes = (int)Math.Ceiling(retryAfter.TotalMinutes);

            return Task.FromResult(RateLimitCheckResult.RateLimited(
                retryAfter,
                $"You've reached your question limit ({limit} per {windowMinutes} minutes). Try again in {minutes} minute(s)."));
        }

        return Task.FromResult(RateLimitCheckResult.Allowed(limit - usageEntry.Count));
    }

    /// <inheritdoc />
    public void RecordUsage(string cacheKeyPrefix, string scopeKey, int windowMinutes)
    {
        var cacheKey = BuildCacheKey(cacheKeyPrefix, scopeKey);
        var entry = _cache.Get<RateLimitUsageEntry>(cacheKey);

        if (entry == null)
        {
            entry = new RateLimitUsageEntry { WindowStart = DateTime.UtcNow, Count = 1 };
        }
        else
        {
            entry.Count++;
        }

        var expiry = entry.WindowStart.AddMinutes(windowMinutes);
        var cacheOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(expiry);

        _cache.Set(cacheKey, entry, cacheOptions);
    }

    private static string BuildCacheKey(string cacheKeyPrefix, string scopeKey) => $"{cacheKeyPrefix}{scopeKey}";

    private class RateLimitUsageEntry
    {
        public DateTime WindowStart { get; set; }
        public int Count { get; set; }
    }
}
