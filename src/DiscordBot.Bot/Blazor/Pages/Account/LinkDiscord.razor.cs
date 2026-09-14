using DiscordBot.Bot.Extensions;
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
/// FAILURE outcomes carry genuinely dynamic legacy text (a verification service's own error
/// message) that a fixed key can't represent losslessly; those pass the exact text through as
/// <see cref="Detail"/> instead of duplicating it in a table - <see cref="RedirectWithStatus"/>
/// only ever appends <c>detail</c> for a failed outcome, precisely so a crafted
/// <c>?status=&lt;success-key&gt;&amp;detail=...</c> URL can never put attacker-chosen text in a
/// SUCCESS banner. <see cref="StatusBanner"/>'s one success case with dynamic text
/// (<c>verify-code-success</c>'s "Welcome, {username}!") instead reads <see cref="DiscordUsername"/>,
/// which <see cref="OnInitializedAsync"/> reloads fresh from the database on the redirect's own
/// GET - by then the verification has already linked the account, so it reflects the real linked
/// username, not anything the client supplied.
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
/// <b>ONE named form for the whole page, dispatched by which submit button fired - two verified
/// static-SSR constraints shape it.</b> An earlier version of this page gave every action
/// (unlink, refresh, initiate/verify/cancel verification) its own
/// <c>&lt;EditForm FormName="..."&gt;</c>, each containing only a plain submit button. That
/// reproduces a real .NET static SSR framework failure this codebase had never hit before -
/// confirmed with curl against this exact page (bypassing any client JS, so not a Playwright/
/// enhanced-navigation artifact) and against a from-scratch minimal repro page, isolating two
/// independent, always-present requirements the framework's static form mapping has for a
/// <c>[SupplyParameterFromForm(FormName = ...)]</c>-bound form to work at all - matching the
/// publicly reported dotnet/aspnetcore issues #55808, #55893, #54854:
/// <list type="number">
/// <item>The form's mapping registration itself never succeeds unless the render also contains at
/// least one real <c>InputBase</c>-derived bound field (<c>InputText</c>, etc.) - a form
/// containing only plain <c>&lt;button name=/value=&gt;</c> pairs and no bound input is never
/// recognized as "a form on this page" at all (400: "Cannot submit the form 'x' because no form
/// on the page currently has that name" - or, once far enough to start rendering it, a 500 inside
/// <c>EndpointHtmlRenderer.ProcessNamedSubmitEventAdditions</c>/<c>FindFormMappingContext</c>,
/// "The renderer does not have a component with ID N"). <see cref="LinkActionFormModel.FormMarker"/>
/// is a hidden, otherwise-unused <c>InputText</c> in each <c>EditForm</c> below that exists solely
/// to satisfy this.
/// </item>
/// <item>A posted field only binds through <c>[SupplyParameterFromForm]</c> when its name carries
/// the exact <c>"{ComponentPropertyName}.{ModelPropertyName}"</c> prefix Blazor's own
/// <c>InputBase</c>-derived components emit for their own bound fields - a bare
/// <c>name="UnlinkAction"</c> is silently dropped; it must be
/// <c>name="ActionForm.UnlinkAction"</c> (verified the same way: posting an unprefixed field came
/// back null, the prefixed one bound correctly).
/// </item>
/// </list>
/// bUnit never caught either constraint because it invokes <c>OnValidSubmit</c> directly against
/// the component instance and never exercises the real static-form-mapping HTTP path (see
/// <c>docs/lessons-learned/blazor-editform-formname-race.md</c> for the same "bUnit can't see
/// this" gap on a different static/interactive-boundary bug). The fix here goes further than just
/// satisfying both constraints per action: exactly one
/// <c>[SupplyParameterFromForm(FormName = "link-discord-actions")]</c>-bound <see cref="ActionForm"/>
/// for the entire page, wrapped in one or two (mutually exclusive per render) <c>&lt;EditForm&gt;</c>
/// elements sharing that same FormName, so there is only ever one form-mapping concern to satisfy
/// per request rather than five. <see cref="HandleFormActionAsync"/> dispatches by which of the
/// per-action <c>*Action</c> fields is non-null - plain HTML's "only the clicked submit button's
/// name/value pair is included in the POST" behaviour, not a Blazor mechanism, so it needs no
/// <see cref="DataAnnotationsValidator"/>/<see cref="Microsoft.AspNetCore.Components.Forms.EditForm.OnValidSubmit"/>
/// gating - <see cref="Microsoft.AspNetCore.Components.Forms.EditForm.OnSubmit"/> always fires and
/// the handler itself decides what to do. The "Link Discord Account" OAuth challenge is still a
/// separate plain <c>&lt;form&gt;</c> posting to a different route entirely
/// (<c>POST /Account/PerformExternalLogin</c>) - HTML forms cannot nest, so it renders as a
/// sibling of <see cref="ActionForm"/>'s <c>EditForm</c>, never inside it.
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

    /// <summary>The page's one and only named form - see the class remarks.</summary>
    [SupplyParameterFromForm(FormName = "link-discord-actions")]
    protected LinkActionFormModel ActionForm { get; set; } = new();

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
    protected const string ReturnUrl = AccountRoutes.LinkDiscord;

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

    /// <summary>
    /// The page's one submit handler - dispatches by which action field the clicked submit
    /// button populated. See the class remarks.
    /// </summary>
    protected async Task HandleFormActionAsync()
    {
        if (User is null)
        {
            return;
        }

        if (ActionForm.UnlinkAction is not null)
        {
            RedirectWithStatus(await LinkService.UnlinkAsync(User));
        }
        else if (ActionForm.RefreshAction is not null)
        {
            RedirectWithStatus(await LinkService.RefreshDiscordDataAsync(User));
        }
        else if (ActionForm.InitiateVerificationAction is not null)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            RedirectWithStatus(await LinkService.InitiateBotVerificationAsync(User, ipAddress));
        }
        else if (ActionForm.CancelVerificationAction is not null)
        {
            RedirectWithStatus(await LinkService.CancelVerificationAsync(User));
        }
        else if (ActionForm.VerifyCodeAction is not null)
        {
            RedirectWithStatus(await LinkService.VerifyCodeAsync(User, ActionForm.VerificationCode));
        }
    }

    private void RedirectWithStatus(DiscordLinkOperationOutcome outcome)
    {
        var url = $"{ReturnUrl}?status={Uri.EscapeDataString(outcome.StatusKey)}";

        // detail is free text that round-trips through the query string, so it is only ever
        // trusted for a FAILURE banner (a service's own error message) - a success banner renders
        // fixed, server-chosen copy so a crafted "?status=verify-code-success&detail=..." URL
        // cannot put attacker text in a green success alert. See the class remarks and
        // StatusBanner below, which reads DiscordUsername (freshly reloaded from the database by
        // OnInitializedAsync on this redirect's own GET) instead of detail for that one case.
        if (!outcome.Succeeded && outcome.Detail is not null)
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
        "verify-code-success" => (true, $"Discord account successfully linked! Welcome, {DiscordUsername ?? "Discord User"}!"),
        "verify-code-failed" => (false, Detail ?? "Invalid verification code."),
        "verify-code-error" => (false, "An error occurred while verifying the code."),
        "cancel-success" => (true, "Verification cancelled."),
        "cancel-error" => (false, "An error occurred while cancelling verification."),
        _ => null
    };

    /// <summary>
    /// The page's one form-bound model. Exactly one of the <c>*Action</c> fields is non-null on
    /// any given submit - populated only by the specific submit button that was clicked (its own
    /// <c>name</c>/<c>value</c> pair), never by data annotations or client script. See the class
    /// remarks.
    /// </summary>
    public sealed class LinkActionFormModel
    {
        /// <summary>
        /// Unused otherwise - exists only so this form always contains at least one real
        /// <c>InputBase</c>-derived bound field, which the framework's static form mapping
        /// requires to register the form at all. See the class remarks.
        /// </summary>
        public string? FormMarker { get; set; } = "1";

        public string? UnlinkAction { get; set; }
        public string? RefreshAction { get; set; }
        public string? InitiateVerificationAction { get; set; }
        public string? CancelVerificationAction { get; set; }
        public string? VerifyCodeAction { get; set; }
        public string? VerificationCode { get; set; }
    }
}
