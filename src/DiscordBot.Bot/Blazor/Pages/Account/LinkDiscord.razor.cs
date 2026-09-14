using System.ComponentModel.DataAnnotations;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Account;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace DiscordBot.Bot.Blazor.Pages.Account;

/// <summary>
/// Code-behind for the static SSR port of <c>Pages/Account/LinkDiscord.cshtml</c> +
/// <c>LinkDiscordModel</c> (docs/plans/blazor-port-plan.md Phase 4 cluster 4c). The GET load
/// (link status, administered guilds, pending verification) is reproduced directly against the
/// same services the legacy page called; every mutation instead goes through
/// <see cref="IDiscordLinkService"/> - see that interface's remarks for why the split falls there.
/// </summary>
/// <remarks>
/// <para>
/// <b>No TempData; status flows through the query string.</b> Same mechanism as
/// <c>Profile.razor.cs</c>'s <c>?status=saved|error</c>, extended to a bigger, page-specific key
/// set (<see cref="Status"/>) because this page has many more distinct outcomes than Profile's
/// two. The banner copy for the fixed majority of outcomes lives in one place, in
/// <see cref="StatusBanner"/> below - mirroring Profile's own <c>@@if (Status == "saved")</c>
/// markup pattern, just centralised into C# because there are ~15 keys instead of 2. A handful of
/// outcomes carry genuinely dynamic legacy text (a verification service's own error message, the
/// linked Discord username) that a fixed key can't represent losslessly; those pass the exact
/// text through as <see cref="Detail"/> instead of duplicating it in a table.
/// </para>
/// <para>
/// <b>Unlink confirmation.</b> Static SSR has no interactive <c>ConfirmModal</c> (see "Auth in
/// components" in <c>docs/architecture/patterns.md</c>: no <c>IJSRuntime</c>, no circuit). This
/// page uses a <c>&lt;details&gt;/&lt;summary&gt;</c> two-step reveal instead (the same
/// no-JS-required disclosure element <c>Blazor/Pages/Error/ServerError.razor</c> already uses for
/// its stack-trace panel) rather than a typed confirmation field: unlinking is disruptive but
/// reversible (re-linking is one OAuth round trip), unlike Privacy's permanent data purge, so a
/// plain second click is proportionate.
/// </para>
/// <para>
/// <b>Every same-page action is an <see cref="EditForm"/> with a distinct <c>FormName</c></b>,
/// including the ones with no real input fields (bound to <see cref="EmptyFormModel"/>) - the only
/// mechanism this codebase has confirmed dispatches a POST to one specific handler on a static SSR
/// page with several of them (see docs/architecture/patterns.md "Blazor Components" and the
/// anthropic-skills:blazor forms reference: <c>FormName</c> is mandatory for SSR forms, and only
/// <c>EditForm</c> auto-wires an <c>OnValidSubmit</c> callback to it). The one exception is the
/// "Link Discord Account" action, which is a plain <c>&lt;form&gt;</c> posting to a *different*
/// route (<c>POST /Account/PerformExternalLogin</c>, the minimal-API Discord challenge endpoint
/// built alongside this cluster) rather than a same-page handler.
/// </para>
/// </remarks>
public partial class LinkDiscord : ComponentBase
{
    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IDiscordTokenService TokenService { get; set; } = default!;

    [Inject]
    private IGuildMembershipService GuildMembershipService { get; set; } = default!;

    [Inject]
    private IVerificationService VerificationService { get; set; } = default!;

    [Inject]
    private IDiscordLinkService LinkService { get; set; } = default!;

    [Inject]
    private DiscordOAuthSettings OAuthSettings { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ILogger<LinkDiscord> Logger { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "status")]
    protected string? Status { get; set; }

    [SupplyParameterFromQuery(Name = "detail")]
    protected string? Detail { get; set; }

    [SupplyParameterFromForm(FormName = "unlink")]
    protected EmptyFormModel UnlinkForm { get; set; } = new();

    [SupplyParameterFromForm(FormName = "refresh-discord-data")]
    protected EmptyFormModel RefreshForm { get; set; } = new();

    [SupplyParameterFromForm(FormName = "initiate-verification")]
    protected EmptyFormModel InitiateVerificationForm { get; set; } = new();

    [SupplyParameterFromForm(FormName = "cancel-verification")]
    protected EmptyFormModel CancelVerificationForm { get; set; } = new();

    [SupplyParameterFromForm(FormName = "verify-code")]
    protected VerifyCodeFormModel VerifyCodeForm { get; set; } = new();

    protected bool UserNotFound { get; private set; }
    protected ApplicationUser? User { get; private set; }

    protected bool IsDiscordOAuthConfigured => OAuthSettings.IsConfigured;
    protected bool IsDiscordLinked { get; private set; }
    protected string? DiscordUsername { get; private set; }
    protected string? DiscordAvatarUrl { get; private set; }
    protected ulong? DiscordUserId { get; private set; }
    protected bool HasValidToken { get; private set; }
    protected IReadOnlyList<DiscordGuildDto> UserGuilds { get; private set; } = Array.Empty<DiscordGuildDto>();
    protected bool HasPendingVerification { get; private set; }
    protected VerificationCode? PendingVerification { get; private set; }

    /// <summary>The return URL the plain "Link Discord Account" form sends to the challenge endpoint.</summary>
    protected const string ReturnUrl = "/Account/LinkDiscord";

    protected override async Task OnInitializedAsync()
    {
        Logger.LogTrace("Entering {MethodName}", nameof(OnInitializedAsync));

        var user = await UserManager.GetUserAsync(HttpContext.User);
        if (user is null)
        {
            Logger.LogWarning("User not found during LinkDiscord page load");
            UserNotFound = true;
            return;
        }

        User = user;
        IsDiscordLinked = user.DiscordUserId.HasValue;
        DiscordUserId = user.DiscordUserId;
        DiscordUsername = user.DiscordUsername;
        DiscordAvatarUrl = user.DiscordAvatarUrl;

        if (IsDiscordLinked)
        {
            try
            {
                HasValidToken = await TokenService.HasValidTokenAsync(user.Id);
                if (HasValidToken)
                {
                    UserGuilds = await GuildMembershipService.GetAdministeredGuildsAsync(user.Id);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error checking token validity or fetching guilds for user {UserId}", user.Id);
            }
        }
        else
        {
            PendingVerification = await VerificationService.GetPendingVerificationAsync(user.Id);
            HasPendingVerification = PendingVerification != null;
        }
    }

    protected async Task HandleUnlinkAsync()
    {
        if (User is null)
        {
            return;
        }

        var outcome = await LinkService.UnlinkAsync(User);
        RedirectWithStatus(outcome);
    }

    protected async Task HandleRefreshAsync()
    {
        if (User is null)
        {
            return;
        }

        var outcome = await LinkService.RefreshDiscordDataAsync(User);
        RedirectWithStatus(outcome);
    }

    protected async Task HandleInitiateVerificationAsync()
    {
        if (User is null)
        {
            return;
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var outcome = await LinkService.InitiateBotVerificationAsync(User, ipAddress);
        RedirectWithStatus(outcome);
    }

    protected async Task HandleVerifyCodeAsync()
    {
        if (User is null)
        {
            return;
        }

        var outcome = await LinkService.VerifyCodeAsync(User, VerifyCodeForm.VerificationCode);
        RedirectWithStatus(outcome);
    }

    protected async Task HandleCancelVerificationAsync()
    {
        if (User is null)
        {
            return;
        }

        var outcome = await LinkService.CancelVerificationAsync(User);
        RedirectWithStatus(outcome);
    }

    private void RedirectWithStatus(DiscordLinkOperationOutcome outcome)
    {
        var url = $"{ReturnUrl}?status={Uri.EscapeDataString(outcome.StatusKey)}";
        if (outcome.Detail is not null)
        {
            url += $"&detail={Uri.EscapeDataString(outcome.Detail)}";
        }

        // Throws NavigationException by design - the mechanism a static SSR handler uses to
        // become a real HTTP redirect (see Profile.razor.cs's own remarks on the same pattern).
        // Never wrap this in try/catch.
        NavigationManager.NavigateTo(url);
    }

    /// <summary>
    /// The single place the fixed-copy status keys map to banner text (success flag + message),
    /// mirroring <c>Profile.razor</c>'s <c>status=saved|error</c> markup pattern. A key not
    /// present here (there are none today) would render nothing; a key whose legacy text is
    /// dynamic uses <see cref="Detail"/> instead of a table entry - see the class remarks.
    /// </summary>
    protected (bool IsSuccess, string Message)? StatusBanner => Status switch
    {
        "unlink-success" => (true, "Discord account unlinked successfully."),
        "not-linked" => (false, "No Discord account is currently linked."),
        "unlink-failed" => (false, "Failed to unlink Discord account. Please try again."),
        "unlink-error" => (false, "An error occurred while unlinking Discord account."),
        "refresh-success" => (true, "Discord data refreshed successfully."),
        "refresh-error" => (false, "An error occurred while refreshing Discord data. Please try again."),
        "verify-init-success" => (true, "Verification initiated. Run /verify-account in Discord to continue."),
        "verify-init-failed" => (false, Detail ?? "Failed to initiate verification."),
        "verify-init-error" => (false, "An error occurred while initiating verification."),
        "verify-code-empty" => (false, "Please enter a verification code."),
        "verify-code-success" => (true, $"Discord account successfully linked! Welcome, {Detail ?? "Discord User"}!"),
        "verify-code-failed" => (false, Detail ?? "Invalid verification code."),
        "verify-code-error" => (false, "An error occurred while verifying the code."),
        "cancel-success" => (true, "Verification cancelled."),
        "cancel-error" => (false, "An error occurred while cancelling verification."),
        _ => null
    };

    /// <summary>Marker model for a same-page <see cref="EditForm"/> with no real input fields.</summary>
    public sealed class EmptyFormModel
    {
    }

    /// <summary>Form-bound model for the verification code entry form.</summary>
    public sealed class VerifyCodeFormModel
    {
        [Required(ErrorMessage = "Please enter a verification code.")]
        public string? VerificationCode { get; set; }
    }
}
