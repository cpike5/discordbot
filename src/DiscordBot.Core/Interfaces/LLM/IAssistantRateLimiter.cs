using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces.LLM;

/// <summary>
/// Shared cache-backed rate limiter for assistant requests. The cache key is always
/// <c>{cacheKeyPrefix}{scopeKey}</c>, so different prefixes (e.g. guild vs DM) can never
/// collide even for an identical scope key.
/// </summary>
public interface IAssistantRateLimiter
{
    /// <summary>
    /// Checks whether the given scope is currently within its rate limit, without recording usage.
    /// </summary>
    Task<RateLimitCheckResult> CheckAsync(
        string cacheKeyPrefix,
        string scopeKey,
        int limit,
        int windowMinutes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records one request against the scope's usage window (call only after a successful request).
    /// </summary>
    void RecordUsage(string cacheKeyPrefix, string scopeKey, int windowMinutes);
}
