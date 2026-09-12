using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Caching.Memory;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <summary>
/// <see cref="IDmSkillActivationStore"/> over <see cref="IMemoryCache"/>, on the same terms as the
/// DM assistant's active guild: a 24-hour entry per user, gone on restart.
/// </summary>
public sealed class DmSkillActivationStore : IDmSkillActivationStore
{
    /// <summary>Cache key prefix, matching the shape of <c>dm_active_guild:</c>.</summary>
    public const string CacheKeyPrefix = "dm_active_skills:";

    /// <summary>How long an activation survives without another message. Matches the active guild's.</summary>
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private readonly IMemoryCache _cache;

    /// <summary>Creates the store.</summary>
    /// <param name="cache">The shared memory cache.</param>
    public DmSkillActivationStore(IMemoryCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Get(ulong userId) =>
        _cache.Get<IReadOnlyList<string>>(CacheKeyPrefix + userId) ?? Array.Empty<string>();

    /// <inheritdoc />
    public void Set(ulong userId, IEnumerable<string> skillKeys)
    {
        ArgumentNullException.ThrowIfNull(skillKeys);

        var keys = skillKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (keys.Count == 0)
        {
            Clear(userId);
            return;
        }

        _cache.Set<IReadOnlyList<string>>(
            CacheKeyPrefix + userId,
            keys,
            new MemoryCacheEntryOptions().SetAbsoluteExpiration(Ttl).SetSize(keys.Count));
    }

    /// <inheritdoc />
    public void Clear(ulong userId) => _cache.Remove(CacheKeyPrefix + userId);
}
