using System.ComponentModel.DataAnnotations;
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
/// <b>Delete-all-data's typed confirmation is ordinary <c>DataAnnotations</c> validation</b>
/// (<see cref="DeleteDataFormModel.Confirmation"/> requires the literal text <c>DELETE</c>), not a
/// redirect-driven status key: a mismatched confirmation never reaches
/// <see cref="HandleDeleteDataAsync"/> at all (<c>EditForm</c>'s <c>OnValidSubmit</c> only fires
/// once <see cref="DataAnnotationsValidator"/> passes), so it renders inline via
/// <c>ValidationMessage</c> on the same request, the same way any other <c>EditForm</c> validation
/// failure does. This replaces the legacy page's client-side <c>quickActions.typedConfirm</c>
/// JS dialog and its <c>fetch('?handler=DeleteData')</c> JSON round trip - see "Auth in
/// components" in <c>docs/architecture/patterns.md</c> for why static SSR has neither
/// <c>IJSRuntime</c> nor a use for that JSON contract. On an actual purge success the user's
/// session ends (<c>SignInManager.SignOutAsync</c>) and the response redirects to
/// <c>/landing</c>, so nothing about the purge itself needs a status banner.
/// </para>
/// <para>
/// <b>Consent toggle forms are pinned to the two <see cref="ConsentType"/> members that exist
/// today</b> (<see cref="MessageLoggingConsentForm"/>/<see cref="AssistantUsageConsentForm"/>) -
/// <c>[SupplyParameterFromForm(FormName = ...)]</c> requires a compile-time constant FormName per
/// bound property, which an open-ended <c>@@foreach</c> over whatever <see cref="ConsentType"/>
/// values the service returns cannot supply. A consent type without a matching form here (none
/// today) renders its status only, with no toggle control - see
/// <see cref="GetConsentFormName"/>'s remarks. Extend this pair, not the loop, when
/// <see cref="ConsentType"/> grows.
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

    [SupplyParameterFromForm(FormName = MessageLoggingFormName)]
    protected EmptyFormModel MessageLoggingConsentForm { get; set; } = new();

    [SupplyParameterFromForm(FormName = AssistantUsageFormName)]
    protected EmptyFormModel AssistantUsageConsentForm { get; set; } = new();

    [SupplyParameterFromForm(FormName = "export-data")]
    protected EmptyFormModel ExportDataForm { get; set; } = new();

    [SupplyParameterFromForm(FormName = "delete-data")]
    protected DeleteDataFormModel DeleteDataForm { get; set; } = new();

    protected const string MessageLoggingFormName = "consent-message-logging";
    protected const string AssistantUsageFormName = "consent-assistant-usage";

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
    /// The FormName for the given <see cref="ConsentType"/> value, or null when there is no form
    /// wired up for it yet - see the class remarks.
    /// </summary>
    protected static string? GetConsentFormName(int type) => (ConsentType)type switch
    {
        ConsentType.MessageLogging => MessageLoggingFormName,
        ConsentType.AssistantUsage => AssistantUsageFormName,
        _ => null
    };

    protected object? GetConsentFormModel(int type) => (ConsentType)type switch
    {
        ConsentType.MessageLogging => MessageLoggingConsentForm,
        ConsentType.AssistantUsage => AssistantUsageConsentForm,
        _ => null
    };

    protected async Task HandleToggleConsentAsync(int type, bool grant)
    {
        Logger.LogTrace("Entering {MethodName} with type={Type}, grant={Grant}", nameof(HandleToggleConsentAsync), type, grant);

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

    protected async Task HandleExportDataAsync()
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
    /// Only reachable once <see cref="DeleteDataFormModel.Confirmation"/> has already validated as
    /// the literal text "DELETE" - see the class remarks.
    /// </summary>
    protected async Task HandleDeleteDataAsync()
    {
        Logger.LogTrace("Entering {MethodName}", nameof(HandleDeleteDataAsync));

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

    /// <summary>Marker model for a same-page <see cref="EditForm"/> with no real input fields.</summary>
    public sealed class EmptyFormModel
    {
    }

    /// <summary>
    /// Form-bound model for the "Delete My Data" form. <see cref="Confirmation"/> must be the
    /// literal text "DELETE" - the server-side replacement for the legacy page's client-side
    /// typed-confirmation JS dialog (see the class remarks).
    /// </summary>
    public sealed class DeleteDataFormModel
    {
        [Required(ErrorMessage = "Type DELETE to confirm.")]
        [RegularExpression("^DELETE$", ErrorMessage = "Type DELETE to confirm.")]
        public string? Confirmation { get; set; }
    }
}
