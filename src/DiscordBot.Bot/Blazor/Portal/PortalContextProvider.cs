using System.Security.Claims;
using DiscordBot.Bot.Services.Portal;

namespace DiscordBot.Bot.Blazor.Portal;

/// <summary>Default <see cref="IPortalContextProvider"/> - a thin memoising wrapper over the
/// existing <see cref="IPortalAccessService"/> (see that interface's remarks for why
/// memoisation is worth having). Mirrors <c>GuildContextProvider</c>'s cache shape exactly.</summary>
public sealed class PortalContextProvider : IPortalContextProvider
{
    private readonly IPortalAccessService _portalAccessService;

    // Memoisation per guildId for the lifetime of this scope - guarded by a lock for the same
    // reason GuildContextProvider's is: a static PortalLayout and the interactive page it wraps
    // can both start resolving during the same prerender pass before either awaits.
    private readonly Dictionary<ulong, Task<PortalAccessResult>> _cache = new();
    private readonly object _cacheLock = new();

    public PortalContextProvider(IPortalAccessService portalAccessService)
    {
        _portalAccessService = portalAccessService;
    }

    public Task<PortalAccessResult> GetAsync(ulong guildId, ClaimsPrincipal user, string returnPath, CancellationToken ct = default)
    {
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(guildId, out var cached))
            {
                return cached;
            }

            var task = _portalAccessService.ResolveAsync(guildId, user, returnPath, ct);
            _cache[guildId] = task;
            return task;
        }
    }
}
