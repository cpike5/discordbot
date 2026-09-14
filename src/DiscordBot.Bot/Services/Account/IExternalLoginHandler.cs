using Microsoft.AspNetCore.Http;

namespace DiscordBot.Bot.Services.Account;

/// <summary>
/// The Discord OAuth callback flow for <c>GET /Account/ExternalLogin/Callback</c>
/// (<see cref="DiscordBot.Bot.Extensions.AccountEndpointExtensions"/>) - the testable half of what
/// used to be <c>Pages/Account/ExternalLogin.cshtml.cs</c>'s <c>OnGetCallbackAsync</c>
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4c). Takes <see cref="HttpContext"/> as an
/// explicit parameter rather than storing an <c>IHttpContextAccessor</c> - the Discord OAuth
/// tokens live on the external-auth cookie (<c>IdentityConstants.ExternalScheme</c>) reachable
/// only through the request's own <see cref="HttpContext"/>, and threading it through the method
/// signature keeps the service trivially unit-testable with a plain <c>DefaultHttpContext</c> the
/// way <c>LoginModelTests</c> tested the page model directly.
/// </summary>
public interface IExternalLoginHandler
{
    /// <summary>
    /// Runs the full callback flow: remote-failure short-circuit, external login info lookup,
    /// token extraction, sign-in-or-create/link, token and guild-membership storage, and audit
    /// logging - see <see cref="ExternalLoginOutcome"/>'s cases for what each branch of the legacy
    /// page model becomes.
    /// </summary>
    /// <param name="httpContext">The current request's <see cref="HttpContext"/> (external-auth cookie, remote IP).</param>
    /// <param name="remoteError">The OAuth provider's own error parameter, if the remote redirect carried one.</param>
    /// <param name="returnUrl">The already-sanitized post-login destination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ExternalLoginOutcome> HandleCallbackAsync(
        HttpContext httpContext,
        string? remoteError,
        string returnUrl,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Closed outcome hierarchy for <see cref="IExternalLoginHandler.HandleCallbackAsync"/>.
/// </summary>
public abstract record ExternalLoginOutcome
{
    private ExternalLoginOutcome()
    {
    }

    /// <summary>Sign-in (existing, newly linked, or newly created user) succeeded - redirect to <paramref name="Url"/>.</summary>
    public sealed record Redirect(string Url) : ExternalLoginOutcome;

    /// <summary>The account is locked out - the caller should send the visitor to <see cref="DiscordBot.Bot.Extensions.AccountRoutes.Lockout"/>.</summary>
    public sealed record Lockout : ExternalLoginOutcome;

    /// <summary>
    /// Every failure path (remote error, missing external login info, missing email claim, a
    /// linking/creation failure) - the caller redirects to
    /// <see cref="DiscordBot.Bot.Extensions.AccountRoutes.Login"/> with <c>?authError=</c> set to
    /// this code. Every case here uses <c>"discord_error"</c>, the same generic bucket
    /// <c>Login</c>'s <c>authError</c> switch already falls back to for anything that isn't
    /// <c>discord_unavailable</c>/<c>discord_expired</c> (those two are classified upstream, in
    /// <c>IdentityServiceExtensions.ClassifyOAuthError</c>, from the OAuth handshake itself - this
    /// handler only ever runs after that handshake already succeeded). Carrying the message via
    /// this code, rather than a plain string a redirect can't carry, fixes the pre-existing bug
    /// where the legacy page set a plain <c>ErrorMessage</c> property that was silently lost
    /// across the very redirect meant to show it (no <c>[TempData]</c> was ever applied to it).
    /// </summary>
    public sealed record Error(string AuthError) : ExternalLoginOutcome;
}
