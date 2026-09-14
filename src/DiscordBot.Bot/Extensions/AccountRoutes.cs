namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Route strings for the account minimal-API endpoints (<see cref="AccountEndpointExtensions"/>)
/// and the static SSR <c>Blazor/Pages/Account/Login.razor</c> page (docs/plans/blazor-port-plan.md
/// Phase 4 cluster 4c). Centralised as public constants because more than one file needs the exact
/// same literal: <c>IdentityConfigOptions</c>'s default <c>LoginPath</c>/<c>LogoutPath</c>/
/// <c>AccessDeniedPath</c>, <c>IdentityServiceExtensions</c>'s <c>OnRemoteFailure</c> redirect,
/// <c>Blazor/Layout/RedirectToLogin.razor</c> and <c>MainNavbar.razor</c>, and - across the
/// concurrent Phase 4c LinkDiscord/Privacy port - <c>Pages/Account/LinkDiscord.cshtml.cs</c>'s own
/// Discord challenge, which posts to <see cref="PerformExternalLogin"/> the same way
/// <c>Blazor/Pages/Account/Login.razor</c>'s Discord button does.
/// </summary>
public static class AccountRoutes
{
    /// <summary>The static SSR sign-in page. Matches <c>IdentityConfigOptions.LoginPath</c>'s default.</summary>
    public const string Login = "/Account/Login";

    /// <summary><c>POST</c> minimal API that signs the current user out. Matches <c>IdentityConfigOptions.LogoutPath</c>'s default.</summary>
    public const string Logout = "/Account/Logout";

    /// <summary>
    /// <c>POST</c> minimal API that issues the Discord OAuth challenge. Used by both the Login
    /// page's "Continue with Discord" button and (via its own literal copy of this route, since
    /// the concurrent LinkDiscord port cannot reference this class from its own worktree) the
    /// LinkDiscord page's "Link Discord" action - a plain <c>&lt;form method="post" action="..."&gt;</c>
    /// with a hidden <c>returnUrl</c> field and an <c>&lt;AntiforgeryToken /&gt;</c> in both cases.
    /// </summary>
    public const string PerformExternalLogin = "/Account/PerformExternalLogin";

    /// <summary><c>GET</c> minimal API - the Discord OAuth callback (<c>SaveTokens</c> reads from here). Not the same as the legacy <c>/signin-discord</c> middleware callback, which is unchanged.</summary>
    public const string ExternalLoginCallback = "/Account/ExternalLogin/Callback";

    /// <summary>The static SSR account-locked page.</summary>
    public const string Lockout = "/Account/Lockout";

    /// <summary>The static SSR access-denied page. Matches <c>IdentityConfigOptions.AccessDeniedPath</c>'s default.</summary>
    public const string AccessDenied = "/Account/AccessDenied";
}
