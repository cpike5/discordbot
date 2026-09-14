using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace DiscordBot.Bot.Blazor.Pages.Account;

/// <summary>
/// Code-behind for the static SSR port of <c>Pages/Account/Privacy.cshtml</c> + <c>PrivacyModel</c>
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4c). Calls <see cref="IConsentService"/>,
/// <see cref="IUserDataExportService"/> and <see cref="IUserPurgeService"/> directly (no wrapper
/// service - unlike LinkDiscord, this page's mutations are each already a single call into one
/// service, so there is nothing a wrapper would centralise).
/// </summary>
/// <remarks>
/// <para>
/// <b>Status banner.</b> Same <c>?status=&amp;detail=</c> mechanism as
/// <c>LinkDiscord.razor.cs</c> - see that class's remarks for the rationale. The one addition here
/// is the export-success banner, whose legacy text (record count, download URL, 7-day expiry
/// note) is entirely dynamic and so is built once, server-side, and carried whole through
/// <see cref="Detail"/>.
/// </para>
/// <para>
/// <b>ONE named form for the whole page, and every button name carries the bound-model
/// prefix.</b> Every consent toggle, "Export My Data" and "Delete My Data" share exactly one
/// <c>[SupplyParameterFromForm(FormName = "privacy-actions")]</c>-bound <see cref="ActionForm"/>,
/// wrapped in one <c>&lt;EditForm&gt;</c> around the entire linked-state card stack - see
/// <c>LinkDiscord.razor.cs</c>'s class remarks for the two verified static-SSR constraints this
/// works around (curl-confirmed against this exact page and a from-scratch minimal repro,
/// matching the publicly reported dotnet/aspnetcore issues #55808, #55893, #54854): a named
/// form's static mapping never registers without at least one real <c>InputBase</c>-derived bound
/// field present in the render (here, <see cref="PrivacyActionFormModel.Confirmation"/>'s
/// <c>InputText</c> - always rendered once linked, since the Data Management card isn't itself
/// conditional, so no separate marker field is needed the way <c>LinkDiscord</c> needs one), and a
/// posted field only binds when its name carries the exact
/// <c>"ActionForm.{ModelPropertyName}"</c> prefix Blazor's own bound inputs emit - a plain
/// <c>name="ExportAction"</c> would be silently dropped; it must be
/// <c>name="ActionForm.ExportAction"</c>. <see cref="HandleFormActionAsync"/> dispatches by which
/// of <see cref="PrivacyActionFormModel.ConsentAction"/> (encoded <c>"{type}:{grant}"</c>, since
/// one consent row's Grant/Revoke button needs to carry *two* values and only the clicked
/// button's own <c>name</c>/<c>value</c> pair is ever posted), <see cref="PrivacyActionFormModel.ExportAction"/>,
/// or <see cref="PrivacyActionFormModel.DeleteAction"/> is non-null - each populated only by its
/// own button, not by data annotations or client script.
/// </para>
/// <para>
/// <b>Delete-all-data's typed confirmation is checked inside <see cref="HandleFormActionAsync"/>
/// itself</b>, not via <c>DataAnnotationsValidator</c> (dropped along with the per-action
/// <c>EditForm</c>s above, since <c>OnSubmit</c> always fires regardless of validity): a
/// mismatched <see cref="PrivacyActionFormModel.Confirmation"/> sets
/// <see cref="DeleteValidationError"/> and returns without redirecting, so the page re-renders in
/// place showing the error - the same "stay on the page, show the problem" outcome
/// <c>ValidationMessage</c> would have given, just decided in C# instead of an attribute. This
/// replaces the legacy page's client-side <c>quickActions.typedConfirm</c> JS dialog and its
/// <c>fetch('?handler=DeleteData')</c> JSON round trip entirely - see "Auth in components" in
/// <c>docs/architecture/patterns.md</c> for why static SSR has neither <c>IJSRuntime</c> nor a use
/// for that JSON contract. On an actual purge success the user's session ends
/// (<c>SignInManager.SignOutAsync</c>) and the response redirects to <c>/landing</c>, so nothing
/// about the purge itself needs a status banner.
/// </para>
/// </remarks>
public partial class Privacy : ComponentBase
{
    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private SignInManager<ApplicationUser> SignInManager { get; set; } = default!;

    [Inject]
    private IConsentService ConsentService { get; set; } = default!;

    [Inject]
    private IUserDataExportService ExportService { get; set; } = default!;

    [Inject]
    private IUserPurgeService PurgeService { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ILogger<Privacy> Logger { get; set; } = default!;

    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "status")]
    protected string? Status { get; set; }

    [SupplyParameterFromQuery(Name = "detail")]
    protected string? Detail { get; set; }

    /// <summary>The page's one and only named form - see the class remarks.</summary>
    [SupplyParameterFromForm(FormName = "privacy-actions")]
    protected PrivacyActionFormModel ActionForm { get; set; } = new();

    /// <summary>
    /// Set by <see cref="HandleFormActionAsync"/> when Delete My Data was submitted with anything
    /// other than the literal text "DELETE" - rendered inline next to the confirmation input
    /// instead of redirecting. See the class remarks.
    /// </summary>
    protected string? DeleteValidationError { get; private set; }

    protected bool UserNotFound { get; private set; }
    protected ApplicationUser? User { get; private set; }
    protected bool IsDiscordLinked { get; private set; }
    protected ulong? DiscordUserId { get; private set; }
    protected string? DiscordUsername { get; private set; }
    protected IReadOnlyList<ConsentStatusDto> ConsentStatuses { get; private set; } = Array.Empty<ConsentStatusDto>();
    protected IReadOnlyList<ConsentHistoryEntryDto> ConsentHistory { get; private set; } = Array.Empty<ConsentHistoryEntryDto>();

    protected override async Task OnInitializedAsync()
    {
        Logger.LogTrace("Entering {MethodName}", nameof(OnInitializedAsync));

        var user = await UserManager.GetUserAsync(HttpContext.User);
        if (user is null)
        {
            Logger.LogWarning("User not found during Privacy page load");
            UserNotFound = true;
            return;
        }

        User = user;
        IsDiscordLinked = user.DiscordUserId.HasValue;
        DiscordUserId = user.DiscordUserId;
        DiscordUsername = user.DiscordUsername;

        if (IsDiscordLinked && DiscordUserId.HasValue)
        {
            try
            {
                ConsentStatuses = (await ConsentService.GetConsentStatusAsync(DiscordUserId.Value)).ToList();
                ConsentHistory = (await ConsentService.GetConsentHistoryAsync(DiscordUserId.Value)).ToList();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error fetching consent data for user {UserId}", user.Id);
            }
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

        if (ActionForm.ConsentAction is not null)
        {
            await HandleToggleConsentAsync(ActionForm.ConsentAction);
        }
        else if (ActionForm.ExportAction is not null)
        {
            await HandleExportDataAsync();
        }
        else if (ActionForm.DeleteAction is not null)
        {
            await HandleDeleteDataAsync();
        }
    }

    /// <param name="encoded">The clicked consent button's own value, "{type}:{grant}".</param>
    private async Task HandleToggleConsentAsync(string encoded)
    {
        Logger.LogTrace("Entering {MethodName} with encoded={Encoded}", nameof(HandleToggleConsentAsync), encoded);

        var parts = encoded.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var type) || !bool.TryParse(parts[1], out var grant))
        {
            RedirectWithStatus("consent-error");
            return;
        }

        if (User is null || !DiscordUserId.HasValue)
        {
            RedirectWithStatus("consent-not-linked");
            return;
        }

        if (!Enum.IsDefined(typeof(ConsentType), type))
        {
            RedirectWithStatus("invalid-consent-type");
            return;
        }

        var consentType = (ConsentType)type;
        var discordUserId = DiscordUserId.Value;

        try
        {
            var result = grant
                ? await ConsentService.GrantConsentAsync(discordUserId, consentType)
                : await ConsentService.RevokeConsentAsync(discordUserId, consentType);

            if (result.Succeeded)
            {
                RedirectWithStatus("consent-updated");
                return;
            }

            var key = result.ErrorCode switch
            {
                ConsentUpdateResult.AlreadyGranted => "consent-already-granted",
                ConsentUpdateResult.NotGranted => "consent-not-granted",
                ConsentUpdateResult.UserNotFound => "consent-user-not-found",
                ConsentUpdateResult.InvalidConsentType => "invalid-consent-type",
                ConsentUpdateResult.DatabaseError => "consent-db-error",
                _ => "consent-failed"
            };
            RedirectWithStatus(key, key == "consent-failed" ? result.ErrorMessage ?? "Failed to update consent preferences. Please try again." : null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error toggling consent for user {UserId}", User.Id);
            RedirectWithStatus("consent-error");
        }
    }

    private async Task HandleExportDataAsync()
    {
        Logger.LogTrace("Entering {MethodName}", nameof(HandleExportDataAsync));

        if (User is null || !DiscordUserId.HasValue)
        {
            RedirectWithStatus("export-not-linked");
            return;
        }

        try
        {
            var result = await ExportService.ExportUserDataAsync(DiscordUserId.Value);

            if (result.Success)
            {
                var totalRecords = result.ExportedCounts.Values.Sum();
                var message = $"Your data has been exported successfully. {totalRecords} records were exported. " +
                              $"Download link: {result.DownloadUrl} (expires in 7 days)";
                RedirectWithStatus("export-success", message);
                return;
            }

            var key = result.ErrorCode switch
            {
                UserDataExportResultDto.UserNotFound => "export-user-not-found",
                UserDataExportResultDto.DatabaseError => "export-db-error",
                UserDataExportResultDto.FileSystemError => "export-fs-error",
                _ => "export-failed"
            };
            RedirectWithStatus(key, key == "export-failed" ? result.ErrorMessage ?? "Failed to export data. Please try again." : null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error exporting data for user {UserId}", User.Id);
            RedirectWithStatus("export-error");
        }
    }

    /// <summary>
    /// Checks the typed confirmation itself (see the class remarks) before doing anything
    /// irreversible.
    /// </summary>
    private async Task HandleDeleteDataAsync()
    {
        Logger.LogTrace("Entering {MethodName}", nameof(HandleDeleteDataAsync));

        if (!string.Equals(ActionForm.Confirmation, "DELETE", StringComparison.Ordinal))
        {
            Logger.LogWarning("User {UserId} submitted delete-data without the typed DELETE confirmation", User!.Id);
            DeleteValidationError = "Type DELETE to confirm.";
            return;
        }

        if (User is null || !DiscordUserId.HasValue)
        {
            RedirectWithStatus("delete-not-linked");
            return;
        }

        var discordUserId = DiscordUserId.Value;

        try
        {
            var (canPurge, reason) = await PurgeService.CanPurgeUserAsync(discordUserId);
            if (!canPurge)
            {
                Logger.LogWarning("Cannot purge user {UserId}: {BlockingReason}", User.Id, reason);
                RedirectWithStatus("delete-blocked", reason ?? "Your data cannot be deleted at this time.");
                return;
            }

            var result = await PurgeService.PurgeUserDataAsync(discordUserId, PurgeInitiator.User, discordUserId.ToString());

            if (!result.Success)
            {
                var key = result.ErrorCode switch
                {
                    UserPurgeResultDto.UserNotFound => "delete-user-not-found",
                    UserPurgeResultDto.UserHasAdminRole => "delete-admin-role",
                    UserPurgeResultDto.DatabaseError => "delete-db-error",
                    UserPurgeResultDto.TransactionFailed => "delete-transaction-failed",
                    _ => "delete-failed"
                };
                RedirectWithStatus(key, key == "delete-failed" ? result.ErrorMessage ?? "Failed to delete data. Please try again." : null);
                return;
            }

            Logger.LogInformation("Successfully deleted data for user {UserId}", User.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting data for user {UserId}", User.Id);
            RedirectWithStatus("delete-error");
            return;
        }

        // The account's data (including its Discord link) has just been purged - end the session
        // and send the visitor to the anonymous landing page, matching the legacy page's redirect
        // to Logout after a successful purge.
        await SignInManager.SignOutAsync();
        NavigationManager.NavigateTo("/landing");
    }

    private void RedirectWithStatus(string statusKey, string? detail = null)
    {
        var url = $"/Account/Privacy?status={Uri.EscapeDataString(statusKey)}";
        if (detail is not null)
        {
            url += $"&detail={Uri.EscapeDataString(detail)}";
        }

        NavigationManager.NavigateTo(url);
    }

    /// <summary>
    /// The single place the fixed-copy status keys map to banner text, mirroring
    /// <c>LinkDiscord.razor.cs</c>'s <c>StatusBanner</c>.
    /// </summary>
    protected (bool IsSuccess, string Message)? StatusBanner => Status switch
    {
        "consent-not-linked" => (false, "You must link your Discord account before managing consent preferences."),
        "invalid-consent-type" => (false, "Invalid consent type."),
        "consent-updated" => (true, "Your consent preferences have been updated successfully."),
        "consent-already-granted" => (false, "This consent is already granted."),
        "consent-not-granted" => (false, "This consent is not currently granted."),
        "consent-user-not-found" => (false, "Discord user not found."),
        "consent-db-error" => (false, "A database error occurred. Please try again."),
        "consent-failed" => (false, Detail ?? "Failed to update consent preferences. Please try again."),
        "consent-error" => (false, "An error occurred while updating consent preferences."),
        "export-not-linked" => (false, "You must link your Discord account before exporting data."),
        "export-success" => (true, Detail ?? "Your data has been exported successfully."),
        "export-user-not-found" => (false, "User not found in the database."),
        "export-db-error" => (false, "A database error occurred. Please try again."),
        "export-fs-error" => (false, "Failed to create export files. Please try again."),
        "export-failed" => (false, Detail ?? "Failed to export data. Please try again."),
        "export-error" => (false, "An error occurred while exporting your data. Please try again."),
        "delete-not-linked" => (false, "You must link your Discord account before deleting data."),
        "delete-blocked" => (false, Detail ?? "Your data cannot be deleted at this time."),
        "delete-user-not-found" => (false, "User not found in the database."),
        "delete-admin-role" => (false, "Cannot delete data for users with admin roles. Contact support."),
        "delete-db-error" => (false, "A database error occurred. Please try again."),
        "delete-transaction-failed" => (false, "Failed to delete data. Please try again."),
        "delete-failed" => (false, Detail ?? "Failed to delete data. Please try again."),
        "delete-error" => (false, "An error occurred while deleting your data. Please try again."),
        _ => null
    };

    /// <summary>
    /// The page's one form-bound model. Exactly one of <see cref="ConsentAction"/>,
    /// <see cref="ExportAction"/> or <see cref="DeleteAction"/> is non-null on any given submit -
    /// populated only by the specific submit button that was clicked (its own
    /// <c>name</c>/<c>value</c> pair), never by data annotations or client script. See the class
    /// remarks.
    /// </summary>
    public sealed class PrivacyActionFormModel
    {
        /// <summary>The clicked consent Grant/Revoke button's own value, encoded "{type}:{grant}".</summary>
        public string? ConsentAction { get; set; }
        public string? ExportAction { get; set; }
        public string? DeleteAction { get; set; }
        public string? Confirmation { get; set; }
    }
}
