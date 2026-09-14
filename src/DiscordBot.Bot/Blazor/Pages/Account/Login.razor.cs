using System.ComponentModel.DataAnnotations;
using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Account;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;

namespace DiscordBot.Bot.Blazor.Pages.Account;

/// <summary>
/// Code-behind for the static SSR port of <c>Pages/Account/Login.cshtml</c> + <c>LoginModel</c>
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4c). The email/password sign-in logic itself
/// lives in <see cref="IPasswordSignInService"/> (unit tested on its own - see
/// <c>PasswordSignInServiceTests</c>); this class is left with only what a static SSR page needs:
/// <c>returnUrl</c>/<c>authError</c> query handling, redirecting an already-authenticated visitor,
/// and turning a <see cref="PasswordSignInOutcome"/> into either a <c>NavigationManager.NavigateTo</c>
/// (success/lockout - thrown by design, kept outside any try/catch, same rule
/// <c>Profile.razor.cs</c>'s remarks document) or a rendered error.
/// </summary>
public partial class Login : ComponentBase
{
    [Inject]
    private IPasswordSignInService SignInService { get; set; } = default!;

    [Inject]
    private DiscordOAuthSettings OAuthSettings { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private IVersionService VersionService { get; set; } = default!;

    [Inject]
    private ILogger<Login> Logger { get; set; } = default!;

    /// <summary>
    /// Supplied automatically for every static SSR page hosted by <c>MapRazorComponents</c> - see
    /// "Auth in components" in <c>docs/architecture/patterns.md</c>.
    /// </summary>
    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnUrlQuery { get; set; }

    /// <summary>OAuth error type from the Discord remote-failure redirect: <c>discord_unavailable</c>, <c>discord_expired</c>, or anything else (treated as <c>discord_error</c>).</summary>
    [SupplyParameterFromQuery(Name = "authError")]
    public string? AuthError { get; set; }

    [SupplyParameterFromForm]
    protected LoginInput Input { get; set; } = new();

    protected bool IsDiscordOAuthConfigured => OAuthSettings.IsConfigured;

    /// <summary>The sanitized post-login destination - see <see cref="SanitizeReturnUrl"/>.</summary>
    protected string ReturnUrl { get; private set; } = "/";

    protected string? AuthErrorTitle { get; private set; }
    protected string? AuthErrorMessage { get; private set; }

    /// <summary>Set after a rejected <see cref="SignInAsync"/> attempt (inactive account or invalid credentials) - re-rendered in place, no redirect, same as the legacy page's <c>PageResult</c>-with-<c>ModelState</c>-error branch.</summary>
    protected string? ErrorMessage { get; private set; }

    protected override void OnInitialized()
    {
        ReturnUrl = SanitizeReturnUrl(ReturnUrlQuery);

        if (HttpContext.User.Identity?.IsAuthenticated == true)
        {
            Logger.LogDebug("Authenticated user redirected from login page");
            NavigationManager.NavigateTo(ReturnUrl);
            return;
        }

        if (!string.IsNullOrEmpty(AuthError))
        {
            (AuthErrorTitle, AuthErrorMessage) = AuthError switch
            {
                "discord_unavailable" => (
                    "Discord is currently unavailable",
                    "Discord's servers appear to be experiencing issues. Please wait a moment and try again."),
                "discord_expired" => (
                    "Login session expired",
                    "Your login session timed out or was already used. Please try signing in again."),
                _ => (
                    "Discord login failed",
                    "Something went wrong during Discord authentication. Please try again.")
            };
        }

        Logger.LogDebug("Login page accessed, return URL: {ReturnUrl}", ReturnUrl);
    }

    protected async Task SignInAsync()
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var outcome = await SignInService.SignInAsync(Input.Email, Input.Password, Input.RememberMe, ReturnUrl, ipAddress);

        switch (outcome)
        {
            case PasswordSignInOutcome.Success success:
                // NavigateTo throws NavigationException by design - the mechanism a static SSR
                // form handler uses to become a real HTTP redirect (see Profile.razor.cs's class
                // remarks). Kept outside any try/catch.
                NavigationManager.NavigateTo(success.ReturnUrl);
                return;
            case PasswordSignInOutcome.LockedOut:
                NavigationManager.NavigateTo(AccountRoutes.Lockout);
                return;
            case PasswordSignInOutcome.Inactive:
                ErrorMessage = "Your account has been deactivated. Please contact an administrator.";
                break;
            case PasswordSignInOutcome.Failed failed:
                ErrorMessage = failed.Message;
                break;
        }
    }

    /// <summary>
    /// Combines <see cref="LocalUrl.IsLocal"/> with <see cref="ReturnUrlHelper.Sanitize"/> the same
    /// way <c>AccountEndpointExtensions.SanitizeReturnUrl</c> does for the minimal-API endpoints -
    /// see <c>Blazor/Pages/Admin/AuditLogs/Details.razor.cs</c> for the earlier precedent this
    /// follows.
    /// </summary>
    private static string SanitizeReturnUrl(string? returnUrl)
        => LocalUrl.IsLocal(returnUrl) ? ReturnUrlHelper.Sanitize(returnUrl, "/") : "/";

    /// <summary>Form-bound model for the email/password form - see <see cref="Input"/>.</summary>
    public sealed class LoginInput
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
