using System.Security.Claims;
using DiscordBot.Bot.Services.Portal;

namespace DiscordBot.Bot.Blazor.Portal;

/// <summary>
/// Resolves the Portal three-state gate for a guild route, memoised per <c>guildId</c> for the
/// lifetime of the current scope - the same shape and reason as
/// <see cref="DiscordBot.Bot.Blazor.Guilds.IGuildContextProvider"/> (see "GuildContext" in
/// <c>docs/architecture/patterns.md</c>): <see cref="PortalAccessService"/> does a real database
/// read (<c>IGuildService.GetGuildByIdAsync</c>) plus, once signed in, a
/// <c>UserManager&lt;ApplicationUser&gt;</c> lookup and either a gateway-cache or REST guild
/// membership check - not free enough to call twice for the one guild id a
/// <c>PortalLayout</c>/<c>PortalPageBase</c> pair both resolve within the same prerender/circuit
/// scope. Registered scoped in <c>AddBlazorUiServices()</c>, the same registration lifetime as
/// <c>IGuildContextProvider</c>.
/// </summary>
public interface IPortalContextProvider
{
    /// <summary>
    /// Resolves the <see cref="PortalAccessResult"/> for <paramref name="guildId"/> and
    /// <paramref name="user"/>. Safe to call repeatedly for the same <paramref name="guildId"/>
    /// within one scope - only the first call does any work.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="user">The visiting user's principal (may be unauthenticated).</param>
    /// <param name="returnPath">
    /// The current request path, forwarded to <see cref="IPortalAccessService.ResolveAsync"/> for
    /// the login URL's <c>returnUrl</c>. Not part of the memoisation key (see the type's own
    /// remarks) - only the first caller for a given <paramref name="guildId"/> within this scope
    /// actually influences the resolved <c>LoginUrl</c>, which in practice is every caller within
    /// one request/circuit, since they all read it off the same current URL.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<PortalAccessResult> GetAsync(ulong guildId, ClaimsPrincipal user, string returnPath, CancellationToken ct = default);
}
