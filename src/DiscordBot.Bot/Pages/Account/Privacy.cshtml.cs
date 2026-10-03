using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Account;

/// <summary>
/// Page model for managing user privacy and consent preferences.
/// Allows authenticated users to view and manage their consent settings for the Discord bot.
/// </summary>
[Authorize]
public class PrivacyModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConsentService _consentService;
    private readonly IUserDataExportService _exportService;
    private readonly IUserPurgeService _purgeService;
    private readonly ILogger<PrivacyModel> _logger;

    public PrivacyModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConsentService consentService,
        IUserDataExportService exportService,
        IUserPurgeService purgeService,
        ILogger<PrivacyModel> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _consentService = consentService;
        _exportService = exportService;
        _purgeService = purgeService;
        _logger = logger;
    }

    /// <summary>
    /// Indicates whether the current user has a Discord account linked.
    /// </summary>
    public bool IsDiscordLinked { get; set; }

    /// <summary>
    /// The Discord user ID (snowflake) of the linked account.
    /// </summary>
    public ulong? DiscordUserId { get; set; }

    /// <summary>
    /// The Discord username of the linked account.
    /// </summary>
    public string? DiscordUsername { get; set; }

    /// <summary>
    /// List of consent statuses for all consent types.
    /// </summary>
    public IEnumerable<ConsentStatusDto> ConsentStatuses { get; set; } = Array.Empty<ConsentStatusDto>();

    /// <summary>
    /// List of consent history entries, ordered by most recent first.
    /// </summary>
    public IEnumerable<ConsentHistoryEntryDto> ConsentHistory { get; set; } = Array.Empty<ConsentHistoryEntryDto>();

    /// <summary>
    /// Page-state error shown when the consent data failed to load.
    /// Action results from the POST handlers are shown as toasts instead.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Id of the export just created, carried across the redirect so the page can show its download button
    /// once (a toast would auto-dismiss). It is an id, not a URL: the link is always built from it here.
    /// </summary>
    [TempData]
    public string? ExportId { get; set; }

    /// <summary>
    /// The link to the authenticated download handler for the export just created, or null when there is none.
    /// Built from the export id (a <see cref="Guid"/>), so it can only ever be a path on this site.
    /// </summary>
    public string? ExportDownloadPath =>
        Guid.TryParse(ExportId, out var exportId) ? Url.Page("/Account/Privacy", "DownloadExport", new { id = exportId }) : null;

    /// <summary>The id of the data-management card, the target of the redirect after an export.</summary>
    private const string DataManagementFragment = "data-management";

    /// <summary>
    /// Redirects back to this page at the given element, so the page does not jump to the top
    /// after a POST from far down the page.
    /// </summary>
    private IActionResult RedirectToFragment(string fragment) =>
        RedirectToPage(pageName: null, pageHandler: null, routeValues: null, fragment: fragment);

    /// <summary>
    /// Handles GET requests to display the privacy and consent settings page.
    /// </summary>
    public async Task<IActionResult> OnGetAsync()
    {
        _logger.LogTrace("Entering {MethodName}", nameof(OnGetAsync));

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during Privacy page load");
            return NotFound("User not found.");
        }

        _logger.LogDebug("Loading privacy settings for user {UserId}", user.Id);

        // Check if Discord is linked
        IsDiscordLinked = user.DiscordUserId.HasValue;
        DiscordUserId = user.DiscordUserId;
        DiscordUsername = user.DiscordUsername;

        if (IsDiscordLinked && DiscordUserId.HasValue)
        {
            _logger.LogDebug("User {UserId} has Discord linked (Discord ID: {DiscordUserId}), fetching consent data",
                user.Id, DiscordUserId.Value);

            try
            {
                // Fetch consent statuses
                ConsentStatuses = await _consentService.GetConsentStatusAsync(DiscordUserId.Value);
                _logger.LogDebug("Retrieved {Count} consent statuses for user {UserId}",
                    ConsentStatuses.Count(), user.Id);

                // Fetch consent history
                ConsentHistory = await _consentService.GetConsentHistoryAsync(DiscordUserId.Value);
                _logger.LogInformation("Retrieved {Count} consent history entries for user {UserId}",
                    ConsentHistory.Count(), user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching consent data for user {UserId}", user.Id);
                ErrorMessage = "An error occurred while loading consent data.";
            }
        }
        else
        {
            _logger.LogDebug("User {UserId} does not have Discord linked", user.Id);
        }

        return Page();
    }

    /// <summary>
    /// Handles POST requests to toggle consent for a specific consent type.
    /// </summary>
    /// <param name="type">The consent type ID to toggle.</param>
    /// <param name="grant">True to grant consent, false to revoke.</param>
    public async Task<IActionResult> OnPostToggleConsentAsync(int type, bool grant)
    {
        _logger.LogTrace("Entering {MethodName} with type={Type}, grant={Grant}",
            nameof(OnPostToggleConsentAsync), type, grant);

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during consent toggle");
            return NotFound("User not found.");
        }

        if (!user.DiscordUserId.HasValue)
        {
            _logger.LogWarning("User {UserId} attempted to toggle consent without Discord account linked", user.Id);
            TempData.SetErrorToast("You must link your Discord account before managing consent preferences.");
            return RedirectToFragment($"consent-{type}");
        }

        // Validate consent type
        if (!Enum.IsDefined(typeof(ConsentType), type))
        {
            _logger.LogWarning("User {UserId} attempted to toggle invalid consent type {Type}", user.Id, type);
            TempData.SetErrorToast("Invalid consent type.");
            return RedirectToFragment($"consent-{type}");
        }

        var consentType = (ConsentType)type;
        var discordUserId = user.DiscordUserId.Value;

        _logger.LogInformation("User {UserId} (Discord ID: {DiscordUserId}) {Action} consent for {ConsentType}",
            user.Id, discordUserId, grant ? "granting" : "revoking", consentType);

        try
        {
            ConsentUpdateResult result;

            if (grant)
            {
                result = await _consentService.GrantConsentAsync(discordUserId, consentType);
            }
            else
            {
                result = await _consentService.RevokeConsentAsync(discordUserId, consentType);
            }

            if (result.Succeeded)
            {
                _logger.LogInformation("Successfully {Action} consent for {ConsentType} for user {UserId}",
                    grant ? "granted" : "revoked", consentType, user.Id);
                TempData.SetSuccessToast("Your consent preferences have been updated successfully.");
            }
            else
            {
                _logger.LogWarning("Failed to {Action} consent for {ConsentType} for user {UserId}: {ErrorCode} - {ErrorMessage}",
                    grant ? "grant" : "revoke", consentType, user.Id, result.ErrorCode, result.ErrorMessage);

                // Handle specific error codes with user-friendly messages
                TempData.SetErrorToast(result.ErrorCode switch
                {
                    ConsentUpdateResult.AlreadyGranted => "This consent is already granted.",
                    ConsentUpdateResult.NotGranted => "This consent is not currently granted.",
                    ConsentUpdateResult.UserNotFound => "Discord user not found.",
                    ConsentUpdateResult.InvalidConsentType => "Invalid consent type.",
                    ConsentUpdateResult.DatabaseError => "A database error occurred. Please try again.",
                    _ => result.ErrorMessage ?? "Failed to update consent preferences. Please try again."
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling consent for user {UserId}", user.Id);
            TempData.SetErrorToast("An error occurred while updating consent preferences.");
        }

        return RedirectToFragment($"consent-{type}");
    }

    /// <summary>
    /// Streams an export archive to the user who created it. The file is resolved only under the signed-in
    /// user's own Discord id, so another user's export id (or an expired or unknown one) is a 404.
    /// </summary>
    /// <param name="id">The export id; anything that is not a GUID never binds and so is a 404 too.</param>
    public async Task<IActionResult> OnGetDownloadExportAsync(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.DiscordUserId is not { } discordUserId)
        {
            return NotFound();
        }

        var path = _exportService.GetExportFilePath(discordUserId, id);
        if (path == null)
        {
            _logger.LogWarning("User {UserId} requested export {ExportId} that does not exist or is not theirs", user.Id, id);
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        var fileName = $"discordbot-data-export-{System.IO.File.GetLastWriteTimeUtc(path):yyyy-MM-dd}.zip";
        return PhysicalFile(path, "application/zip", fileName);
    }

    /// <summary>
    /// Handles POST requests to export user data.
    /// </summary>
    public async Task<IActionResult> OnPostExportDataAsync()
    {
        _logger.LogTrace("Entering {MethodName}", nameof(OnPostExportDataAsync));

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during export data");
            return NotFound("User not found.");
        }

        if (!user.DiscordUserId.HasValue)
        {
            _logger.LogWarning("User {UserId} attempted to export data without Discord account linked", user.Id);
            TempData.SetErrorToast("You must link your Discord account before exporting data.");
            return RedirectToFragment(DataManagementFragment);
        }

        var discordUserId = user.DiscordUserId.Value;

        _logger.LogInformation("User {UserId} (Discord ID: {DiscordUserId}) initiated data export from web UI",
            user.Id, discordUserId);

        try
        {
            var result = await _exportService.ExportUserDataAsync(discordUserId);

            if (result.Success)
            {
                var totalRecords = result.ExportedCounts.Values.Sum();

                _logger.LogInformation("Successfully exported data for user {UserId}. {RecordCount} records exported",
                    user.Id, totalRecords);

                TempData.SetSuccessToast($"Your data has been exported. It has {DisplayFormat.Plural(totalRecords, "record")}.");
                ExportId = result.ExportId?.ToString();
            }
            else
            {
                _logger.LogWarning("Failed to export data for user {UserId}: {ErrorCode} - {ErrorMessage}",
                    user.Id, result.ErrorCode, result.ErrorMessage);

                TempData.SetErrorToast(result.ErrorCode switch
                {
                    UserDataExportResultDto.UserNotFound => "User not found in the database.",
                    UserDataExportResultDto.DatabaseError => "A database error occurred. Please try again.",
                    UserDataExportResultDto.FileSystemError => "Failed to create export files. Please try again.",
                    _ => result.ErrorMessage ?? "Failed to export data. Please try again."
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting data for user {UserId}", user.Id);
            TempData.SetErrorToast("An error occurred while exporting your data. Please try again.");
        }

        return RedirectToFragment(DataManagementFragment);
    }

    /// <summary>
    /// Handles POST requests to delete user data.
    /// Returns JSON for AJAX consumption by the typed confirmation dialog flow.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteDataAsync()
    {
        _logger.LogTrace("Entering {MethodName}", nameof(OnPostDeleteDataAsync));

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during delete data");
            return new JsonResult(new { success = false, message = "User not found." })
            {
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        if (!user.DiscordUserId.HasValue)
        {
            _logger.LogWarning("User {UserId} attempted to delete data without Discord account linked", user.Id);
            return new JsonResult(new { success = false, message = "You must link your Discord account before deleting data." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var discordUserId = user.DiscordUserId.Value;

        _logger.LogInformation("User {UserId} (Discord ID: {DiscordUserId}) initiated data deletion from web UI",
            user.Id, discordUserId);

        try
        {
            // Check if user can be purged
            var (canPurge, reason) = await _purgeService.CanPurgeUserAsync(discordUserId);
            if (!canPurge)
            {
                _logger.LogWarning("Cannot purge user {UserId}: {BlockingReason}", user.Id, reason);
                return new JsonResult(new { success = false, message = reason ?? "Your data cannot be deleted at this time." })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }

            var result = await _purgeService.PurgeUserDataAsync(
                discordUserId,
                PurgeInitiator.User,
                discordUserId.ToString());

            if (result.Success)
            {
                var totalDeleted = result.DeletedCounts.Values.Sum();

                _logger.LogInformation("Successfully deleted data for user {UserId}. {RecordCount} records deleted",
                    user.Id, totalDeleted);

                // The user's data has been purged, so end the session here rather than trusting
                // the client to reach the logout page. The redirect lands on the signed-out page.
                await _signInManager.SignOutAsync();

                // Return JSON with a redirect URL so the client can navigate after showing the toast.
                return new JsonResult(new
                {
                    success = true,
                    message = $"Your data has been permanently deleted. {DisplayFormat.Plural(totalDeleted, "record")} removed.",
                    redirectUrl = Url.Page("/Account/Logout") ?? "/"
                });
            }
            else
            {
                _logger.LogWarning("Failed to delete data for user {UserId}: {ErrorCode} - {ErrorMessage}",
                    user.Id, result.ErrorCode, result.ErrorMessage);

                var errorMessage = result.ErrorCode switch
                {
                    UserPurgeResultDto.UserNotFound => "User not found in the database.",
                    UserPurgeResultDto.UserHasAdminRole => "Cannot delete data for users with admin roles. Contact support.",
                    UserPurgeResultDto.DatabaseError => "A database error occurred. Please try again.",
                    UserPurgeResultDto.TransactionFailed => "Failed to delete data. Please try again.",
                    _ => result.ErrorMessage ?? "Failed to delete data. Please try again."
                };

                return new JsonResult(new { success = false, message = errorMessage })
                {
                    StatusCode = StatusCodes.Status422UnprocessableEntity
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting data for user {UserId}", user.Id);
            return new JsonResult(new { success = false, message = "An error occurred while deleting your data. Please try again." })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
