using DiscordBot.Core.Entities;

namespace DiscordBot.Bot.Services.Account;

/// <summary>
/// Wraps the mutation handlers <c>Pages/Account/LinkDiscord.cshtml.cs</c> used to expose (unlink,
/// refresh Discord data, initiate/verify/cancel bot verification) behind a single, page-agnostic
/// service so the static SSR <c>Blazor/Pages/Account/LinkDiscord.razor.cs</c> can call one
/// dependency instead of five (<c>IDiscordTokenService</c>, <c>IDiscordUserInfoService</c>,
/// <c>IUserDiscordGuildService</c>, <c>IVerificationService</c>, <c>UserManager</c>) directly, and
/// so the behaviour is unit-testable without a Razor Pages/Blazor hosting model at all - see
/// <c>docs/plans/blazor-port-plan.md</c> Phase 4 cluster 4c.
/// </summary>
/// <remarks>
/// Deliberately does not wrap the GET load (link status, administered guilds, pending
/// verification) or the OAuth challenge itself (<c>OnPostLinkAsync</c>) - the former is a plain
/// read the page performs directly against the same services on <c>OnInitializedAsync</c> (there
/// is nothing to test in isolation that unit tests didn't already cover via the individual
/// services), and the latter becomes a plain HTML form posting to the
/// <c>POST /Account/PerformExternalLogin</c> minimal-API endpoint (cluster 4c's other agent),
/// which this page never calls in-process.
/// </remarks>
public interface IDiscordLinkService
{
    /// <summary>
    /// Unlinks <paramref name="user"/>'s Discord account: clears the Discord fields on the user,
    /// deletes OAuth tokens and stored guild memberships, invalidates the Discord user-info cache,
    /// and removes the "Discord" external login. Mirrors
    /// <c>LinkDiscordModel.OnPostUnlinkAsync</c> exactly, including its "no-op with a friendly
    /// message" behaviour when nothing is linked and its "still succeeds" behaviour when removing
    /// the external login fails or none exists.
    /// </summary>
    Task<DiscordLinkOperationOutcome> UnlinkAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-fetches <paramref name="user"/>'s Discord guild memberships from the Discord API and
    /// invalidates the Discord user-info cache. Mirrors
    /// <c>LinkDiscordModel.OnPostRefreshDiscordDataAsync</c>.
    /// </summary>
    Task<DiscordLinkOperationOutcome> RefreshDiscordDataAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a bot-verification request for <paramref name="user"/>. Mirrors
    /// <c>LinkDiscordModel.OnPostInitiateBotVerificationAsync</c>; on failure,
    /// <see cref="DiscordLinkOperationOutcome.Detail"/> carries the verification service's own
    /// error text (result.ErrorMessage), exactly as the legacy page forwarded it.
    /// </summary>
    Task<DiscordLinkOperationOutcome> InitiateBotVerificationAsync(ApplicationUser user, string? ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a verification code entered by <paramref name="user"/> and links the Discord
    /// account on success. Mirrors <c>LinkDiscordModel.OnPostVerifyCodeAsync</c>, including the
    /// hyphen/space stripping and upper-casing performed on the entered code before validation. On
    /// failure, <see cref="DiscordLinkOperationOutcome.Detail"/> carries the verification
    /// service's own error text; on success it is <see langword="null"/> - the caller renders the
    /// linked username by reading it back from the database instead, so a crafted redirect URL
    /// cannot spoof the success banner's text (see <see cref="DiscordLinkOperationOutcome.Detail"/>).
    /// </summary>
    Task<DiscordLinkOperationOutcome> VerifyCodeAsync(ApplicationUser user, string? code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels any pending verification for <paramref name="user"/>. Mirrors
    /// <c>LinkDiscordModel.OnPostCancelVerificationAsync</c>.
    /// </summary>
    Task<DiscordLinkOperationOutcome> CancelVerificationAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of one <see cref="IDiscordLinkService"/> mutation.
/// </summary>
/// <param name="Succeeded">Whether the operation succeeded (matches the legacy page's <c>IsSuccess</c>).</param>
/// <param name="StatusKey">
/// A short, stable slug identifying which outcome occurred (e.g. <c>"unlink-success"</c>,
/// <c>"not-linked"</c>) - the page's static-SSR redirect target uses this as its
/// <c>?status=</c> value and looks up the exact banner copy for it in one place in the page's own
/// markup (mirroring <c>Blazor/Pages/Account/Profile.razor</c>'s <c>status=saved|error</c>
/// pattern), rather than round-tripping arbitrary text through the query string for the fixed
/// majority of outcomes.
/// </param>
/// <param name="Detail">
/// Present only for the handful of FAILURE outcomes whose legacy banner text is genuinely dynamic
/// at runtime (a service's own error message) - null for every outcome whose text is fixed and
/// already covered by <see cref="StatusKey"/> alone, and always null on success: the caller
/// (<c>LinkDiscord.razor.cs</c>'s <c>RedirectWithStatus</c>) never forwards <see cref="Detail"/>
/// into a success redirect's query string even if a future outcome set it, since that string
/// would otherwise render verbatim inside a SUCCESS alert with no server-side check that it's
/// real - a spoofable green banner. A success banner with dynamic text (the linked Discord
/// username) instead reads it back from the database.
/// </param>
public sealed record DiscordLinkOperationOutcome(bool Succeeded, string StatusKey, string? Detail = null);
