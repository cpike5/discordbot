using System.Security.Claims;

namespace DiscordBot.Bot.Services.Portal;

/// <summary>
/// The Portal three-state access gate (anonymous/non-member landing page → not-a-member forbidden
/// → member portal), extracted from <c>Pages/Portal/PortalPageModelBase.CheckPortalAuthorizationAsync</c>
/// so a future Blazor <c>PortalLayout</c> can reuse the exact same behavior the three Razor Pages
/// Portal Index pages (Soundboard/TTS/VOX) rely on today. See "Portal three-state gate" in
/// <c>docs/architecture/patterns.md</c>.
/// </summary>
public interface IPortalAccessService
{
    /// <summary>
    /// Resolves portal access for <paramref name="guildId"/> and <paramref name="user"/>.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="user">The visiting user's principal (may be unauthenticated).</param>
    /// <param name="returnPath">
    /// The current request path, used to build the login URL's <c>returnUrl</c> query parameter
    /// exactly as <c>PortalPageModelBase</c> does today (<c>HttpContext.Request.Path</c>).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<PortalAccessResult> ResolveAsync(ulong guildId, ClaimsPrincipal user, string returnPath, CancellationToken ct = default);
}
