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
    /// A read-only peek for callers that want to refuse early; it reserves nothing, so a request
    /// that is going to run must still call <see cref="TryReserveAsync"/>.
    /// </summary>
    Task<RateLimitCheckResult> CheckAsync(
        string cacheKeyPrefix,
        string scopeKey,
        int limit,
        int windowMinutes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the limit and, when allowed, takes one slot in the same atomic step, so parallel
    /// requests from one scope cannot all pass the check before any of them is counted. Call
    /// <see cref="Release"/> if the request then fails: only successful requests count.
    /// </summary>
    Task<RateLimitCheckResult> TryReserveAsync(
        string cacheKeyPrefix,
        string scopeKey,
        int limit,
        int windowMinutes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives back a slot taken by <see cref="TryReserveAsync"/> for a request that did not succeed.
    /// </summary>
    void Release(string cacheKeyPrefix, string scopeKey);
}
