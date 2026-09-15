namespace DiscordBot.Bot.Services.Account;

/// <summary>
/// Email/password sign-in for the static SSR <c>Blazor/Pages/Account/Login.razor</c> page
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4c) - the testable half of what used to be
/// <c>Pages/Account/Login.cshtml.cs</c>'s <c>OnPostAsync</c>. Kept free of anything Blazor- or
/// HTTP-specific (no <c>HttpContext</c>, no <c>IActionResult</c>) so it can be unit tested the same
/// way <c>LoginModelTests</c> tested the page model directly, and reused by the endpoint layer if
/// a non-Blazor caller ever needs it.
/// </summary>
public interface IPasswordSignInService
{
    /// <summary>
    /// Attempts an email/password sign-in with lockout enabled, exactly as
    /// <c>LoginModel.OnPostAsync</c> did: rejects a deactivated (<c>IsActive == false</c>) user
    /// before ever calling <see cref="Microsoft.AspNetCore.Identity.SignInManager{TUser}"/>,
    /// updates <c>LastLoginAt</c> and writes an audit log entry on success, writes an audit log
    /// entry on every rejected attempt (inactive user aside - that path was never audited by the
    /// legacy page and still isn't), and treats <c>SignInResult.RequiresTwoFactor</c> as an
    /// ordinary failure (see <see cref="PasswordSignInOutcome.Failed"/>'s remarks) since no 2FA
    /// flow exists in this application.
    /// </summary>
    /// <param name="email">The submitted email address.</param>
    /// <param name="password">The submitted password.</param>
    /// <param name="rememberMe">Whether the sign-in cookie should be persistent.</param>
    /// <param name="returnUrl">
    /// The already-sanitized post-login destination (see <c>LocalUrl.IsLocal</c>/
    /// <c>ReturnUrlHelper.Sanitize</c> in the caller) - echoed back unchanged on
    /// <see cref="PasswordSignInOutcome.Success"/> purely so the caller has one value to act on.
    /// </param>
    /// <param name="ipAddress">The caller's IP address for the audit log entry, or <see langword="null"/> when unavailable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PasswordSignInOutcome> SignInAsync(
        string email,
        string password,
        bool rememberMe,
        string returnUrl,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Closed outcome hierarchy for <see cref="IPasswordSignInService.SignInAsync"/> - a private
/// constructor keeps every case a nested <see langword="sealed"/> record, so a caller's
/// <see langword="switch"/> over the concrete types is exhaustive.
/// </summary>
public abstract record PasswordSignInOutcome
{
    private PasswordSignInOutcome()
    {
    }

    /// <summary>Sign-in succeeded. <paramref name="ReturnUrl"/> is the same value the caller passed in.</summary>
    public sealed record Success(string ReturnUrl) : PasswordSignInOutcome;

    /// <summary>The user exists but <c>IsActive</c> is <see langword="false"/> - rejected before any sign-in attempt.</summary>
    public sealed record Inactive : PasswordSignInOutcome;

    /// <summary>
    /// <see cref="Microsoft.AspNetCore.Identity.SignInResult.IsLockedOut"/> was <see langword="true"/> -
    /// the caller should send the visitor to <see cref="DiscordBot.Bot.Extensions.AccountRoutes.Lockout"/>.
    /// </summary>
    public sealed record LockedOut : PasswordSignInOutcome;

    /// <summary>
    /// Every other rejection: wrong credentials, or <c>SignInResult.RequiresTwoFactor</c> (no 2FA
    /// flow exists in this application - the legacy page's dead <c>RedirectToPage("./LoginWith2fa")</c>
    /// branch, which targeted a page that was never implemented, is removed rather than ported;
    /// this case is logged distinctly server-side but shown the same generic message as any other
    /// failed sign-in, so a caller can't use it to probe whether 2FA would otherwise have applied).
    /// </summary>
    public sealed record Failed(string Message) : PasswordSignInOutcome;
}
