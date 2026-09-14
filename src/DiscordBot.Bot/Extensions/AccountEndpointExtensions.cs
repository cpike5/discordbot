using System.Security.Claims;
using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Account;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// The three minimal-API endpoints behind sign-in/sign-out (docs/plans/blazor-port-plan.md Phase 4
/// cluster 4c), replacing <c>Pages/Account/{Login,ExternalLogin,Logout}.cshtml.cs</c>'s POST/GET
/// handlers now that those pages are the static SSR <c>Blazor/Pages/Account/Login.razor</c> plus
/// these endpoints. Mapped from <c>Program.cs</c> next to <c>MapRazorPages()</c>/
/// <c>MapRazorComponents</c>, all <c>[AllowAnonymous]</c> since a signed-out visitor must be able
/// to reach every one of them.
/// </summary>
/// <remarks>
/// <b>Antiforgery on <c>POST /Account/Logout</c> and <c>POST /Account/PerformExternalLogin</c>.</b>
/// Both bind a field with <c>[FromForm]</c>. Since ASP.NET Core 8, a minimal API endpoint that
/// binds any parameter from form data is automatically decorated with the same antiforgery
/// requirement <c>[ValidateAntiForgeryToken]</c>/<c>asp-antiforgery</c> apply to a Razor Pages
/// handler - <c>IAntiforgeryMetadata.RequiredI</c> defaults to <see langword="true"/> for such an
/// endpoint, and <c>app.UseAntiforgery()</c> (already in the pipeline for Blazor's own
/// <c>EditForm</c>/<c>AntiforgeryToken</c> support - see "Blazor Components" &gt; "Hosting model"
/// in <c>docs/architecture/patterns.md</c>) enforces it for both stacks without double-validating.
/// No <c>[ValidateAntiForgeryToken]</c>/<c>DisableAntiforgery()</c> call is needed or present here;
/// confirmed by <c>AccountEndpointExtensionsTests</c> and by the E2E logout/login round trip, both
/// of which fail with a 400 if the posted <c>&lt;AntiforgeryToken /&gt;</c>/hidden field is
/// missing or stale.
/// </remarks>
public static class AccountEndpointExtensions
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(AccountRoutes.Logout, HandleLogoutAsync)
            .AllowAnonymous();

        app.MapPost(AccountRoutes.PerformExternalLogin, HandlePerformExternalLogin)
            .AllowAnonymous();

        app.MapGet(AccountRoutes.ExternalLoginCallback, HandleExternalLoginCallbackAsync)
            .AllowAnonymous();

        // Legacy GET /Account/ExternalLogin (no handler) - the page itself only ever redirected
        // to Login; external login requires the POST challenge above.
        app.MapGet("/Account/ExternalLogin", () => Results.LocalRedirect(AccountRoutes.Login))
            .AllowAnonymous();

        return app;
    }

    internal static async Task<IResult> HandleLogoutAsync(
        HttpContext httpContext,
        [FromForm] string? returnUrl,
        SignInManager<ApplicationUser> signInManager,
        IAuditLogService auditLogService,
        ILogger<Program> logger)
    {
        var userName = httpContext.User.Identity?.Name;
        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Audit log BEFORE signing out (while we still have user context).
        if (!string.IsNullOrEmpty(userId))
        {
            try
            {
                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                auditLogService.CreateBuilder()
                    .ForCategory(AuditLogCategory.Security)
                    .WithAction(AuditLogAction.Logout)
                    .ByUser(userId)
                    .OnTarget("User", userId)
                    .FromIpAddress(ipAddress ?? "Unknown")
                    .WithDetails(new { userName })
                    .Enqueue();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to log audit entry for logout {UserName}", userName);
            }
        }

        await signInManager.SignOutAsync();

        logger.LogInformation("User {UserName} logged out", userName ?? "Unknown");

        var target = string.IsNullOrWhiteSpace(returnUrl) ? "/landing" : SanitizeReturnUrl(returnUrl, "/landing");
        return Results.LocalRedirect(target);
    }

    internal static IResult HandlePerformExternalLogin(
        [FromForm] string? returnUrl,
        SignInManager<ApplicationUser> signInManager,
        DiscordOAuthSettings oauthSettings)
    {
        var sanitizedReturnUrl = SanitizeReturnUrl(returnUrl, "/");

        if (!oauthSettings.IsConfigured)
        {
            return Results.LocalRedirect($"{AccountRoutes.Login}?authError=discord_error");
        }

        var redirectUri = $"{AccountRoutes.ExternalLoginCallback}?returnUrl={Uri.EscapeDataString(sanitizedReturnUrl)}";
        var properties = signInManager.ConfigureExternalAuthenticationProperties("Discord", redirectUri);
        return Results.Challenge(properties, [DiscordAuthenticationSchemeName]);
    }

    internal static async Task<IResult> HandleExternalLoginCallbackAsync(
        HttpContext httpContext,
        string? returnUrl,
        string? remoteError,
        IExternalLoginHandler handler)
    {
        var sanitizedReturnUrl = SanitizeReturnUrl(returnUrl, "/");

        var outcome = await handler.HandleCallbackAsync(httpContext, remoteError, sanitizedReturnUrl, httpContext.RequestAborted);

        return outcome switch
        {
            ExternalLoginOutcome.Redirect redirect => Results.LocalRedirect(redirect.Url),
            ExternalLoginOutcome.Lockout => Results.LocalRedirect(AccountRoutes.Lockout),
            ExternalLoginOutcome.Error error =>
                Results.LocalRedirect($"{AccountRoutes.Login}?authError={error.AuthError}&returnUrl={Uri.EscapeDataString(sanitizedReturnUrl)}"),
            _ => Results.LocalRedirect(AccountRoutes.Login)
        };
    }

    /// <summary>
    /// The Discord scheme name registered by <c>IdentityServiceExtensions.AddDiscordOAuth</c>
    /// (<c>AspNet.Security.OAuth.Discord</c>'s <c>DiscordAuthenticationDefaults.AuthenticationScheme</c>,
    /// which is the literal string <c>"Discord"</c> - duplicated here rather than referencing the
    /// package type to keep this file's usings minimal; <c>PerformExternalLoginRedirectsToDiscordChallenge</c>
    /// guards the literal matches the package constant).
    /// </summary>
    private const string DiscordAuthenticationSchemeName = "Discord";

    /// <summary>
    /// Combines <see cref="LocalUrl.IsLocal"/> (rejects <c>javascript:</c>/absolute/protocol-relative
    /// URLs - an open-redirect/XSS guard on a value that arrived as a query string or posted form
    /// field) with <see cref="ReturnUrlHelper.Sanitize"/> (rejects a URL that points back at the
    /// login page itself, which would loop or 404 once the visitor is authenticated).
    /// </summary>
    internal static string SanitizeReturnUrl(string? returnUrl, string fallback)
        => LocalUrl.IsLocal(returnUrl) ? ReturnUrlHelper.Sanitize(returnUrl, fallback) : fallback;
}
