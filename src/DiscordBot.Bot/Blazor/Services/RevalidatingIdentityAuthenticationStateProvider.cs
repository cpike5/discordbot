using System.Security.Claims;
using DiscordBot.Core.Entities;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Blazor.Services;

/// <summary>
/// Re-validates the circuit's authenticated user on a fixed interval instead of relying on the
/// auth cookie, which a long-lived circuit outlives, or <c>DiscordClaimsTransformation</c>,
/// which only runs per HTTP request and never fires again once a circuit is established (see
/// "Auth state in circuits" in <c>docs/plans/blazor-port-plan.md</c> §4.2).
/// </summary>
/// <remarks>
/// A user fails revalidation - and is signed out of the circuit - when any of the following is
/// true: the user no longer exists (deleted), the user is currently locked out (banned via the
/// existing lockout mechanism), or, when the user store supports security stamps, the stamp
/// claim carried on the circuit's principal no longer matches the user's current stamp (password
/// change, role change, or an explicit stamp refresh elsewhere in the app). A failed
/// revalidation does not retroactively affect requests already served by this circuit; it only
/// prevents the circuit from continuing to be usable, forcing a fresh sign-in on next navigation.
/// </remarks>
public sealed class RevalidatingIdentityAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> optionsAccessor)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IdentityOptions _options = optionsAccessor.Value;

    /// <inheritdoc />
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    /// <inheritdoc />
    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        // Resolve UserManager from a fresh scope rather than the circuit's own scope, so the
        // lookup reads current data instead of anything EF may have cached in the circuit's
        // long-lived DbContext usage.
        await using var scope = _scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return await ValidateUserAsync(userManager, authenticationState.User);
    }

    /// <summary>
    /// The testable core of <see cref="ValidateAuthenticationStateAsync"/>: exists/lockout/stamp
    /// checks against an already-resolved <see cref="UserManager{TUser}"/>, so tests can supply a
    /// mocked manager directly instead of going through a DI scope.
    /// </summary>
    internal async Task<bool> ValidateUserAsync(UserManager<ApplicationUser> userManager, ClaimsPrincipal principal)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return false;
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return false;
        }

        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        var principalStamp = principal.FindFirstValue(_options.ClaimsIdentity.SecurityStampClaimType);
        var userStamp = await userManager.GetSecurityStampAsync(user);
        return principalStamp == userStamp;
    }
}
