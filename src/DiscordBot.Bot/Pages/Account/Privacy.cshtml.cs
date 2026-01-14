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
    private readonly IConsentService _consentService;
    private readonly IUserDataExportService _exportService;
    private readonly IUserPurgeService _purgeService;
    private readonly ILogger<PrivacyModel> _logger;

    public PrivacyModel(
        UserManager<ApplicationUser> userManager,
        IConsentService consentService,
        IUserDataExportService exportService,
        IUserPurgeService purgeService,
        ILogger<PrivacyModel> logger)
    {
        _userManager = userManager;
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
    /// Status message to display to the user (success or error).
    /// </summary>
    [TempData]
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Indicates whether the status message is a success message.
    /// </summary>
    [TempData]
    public bool IsSuccess { get; set; }

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
                StatusMessage = "An error occurred while loading consent data.";
                IsSuccess = false;
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
            StatusMessage = "You must link your Discord account before managing consent preferences.";
            IsSuccess = false;
            return RedirectToPage();
        }

        // Validate consent type
        if (!Enum.IsDefined(typeof(ConsentType), type))
        {
            _logger.LogWarning("User {UserId} attempted to toggle invalid consent type {Type}", user.Id, type);
            StatusMessage = "Invalid consent type.";
            IsSuccess = false;
            return RedirectToPage();
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
                StatusMessage = "Your consent preferences have been updated successfully.";
                IsSuccess = true;
            }
            else
            {
                _logger.LogWarning("Failed to {Action} consent for {ConsentType} for user {UserId}: {ErrorCode} - {ErrorMessage}",
                    grant ? "grant" : "revoke", consentType, user.Id, result.ErrorCode, result.ErrorMessage);

                // Handle specific error codes with user-friendly messages
                StatusMessage = result.ErrorCode switch
                {
                    ConsentUpdateResult.AlreadyGranted => "This consent is already granted.",
                    ConsentUpdateResult.NotGranted => "This consent is not currently granted.",
                    ConsentUpdateResult.UserNotFound => "Discord user not found.",
                    ConsentUpdateResult.InvalidConsentType => "Invalid consent type.",
                    ConsentUpdateResult.DatabaseError => "A database error occurred. Please try again.",
                    _ => result.ErrorMessage ?? "Failed to update consent preferences. Please try again."
                };
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling consent for user {UserId}", user.Id);
            StatusMessage = "An error occurred while updating consent preferences.";
            IsSuccess = false;
        }

        return RedirectToPage();
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
            StatusMessage = "You must link your Discord account before exporting data.";
            IsSuccess = false;
            return RedirectToPage();
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

                StatusMessage = $"Your data has been exported successfully. {totalRecords} records were exported. " +
                              $"Download link: {result.DownloadUrl} (expires in 7 days)";
                IsSuccess = true;
            }
            else
            {
                _logger.LogWarning("Failed to export data for user {UserId}: {ErrorCode} - {ErrorMessage}",
                    user.Id, result.ErrorCode, result.ErrorMessage);

                StatusMessage = result.ErrorCode switch
                {
                    UserDataExportResultDto.UserNotFound => "User not found in the database.",
                    UserDataExportResultDto.DatabaseError => "A database error occurred. Please try again.",
                    UserDataExportResultDto.FileSystemError => "Failed to create export files. Please try again.",
                    _ => result.ErrorMessage ?? "Failed to export data. Please try again."
                };
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting data for user {UserId}", user.Id);
            StatusMessage = "An error occurred while exporting your data. Please try again.";
            IsSuccess = false;
        }

        return RedirectToPage();
    }

    /// <summary>
    /// Handles POST requests to delete user data.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteDataAsync()
    {
        _logger.LogTrace("Entering {MethodName}", nameof(OnPostDeleteDataAsync));

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            _logger.LogWarning("User not found during delete data");
            return NotFound("User not found.");
        }

        if (!user.DiscordUserId.HasValue)
        {
            _logger.LogWarning("User {UserId} attempted to delete data without Discord account linked", user.Id);
            StatusMessage = "You must link your Discord account before deleting data.";
            IsSuccess = false;
            return RedirectToPage();
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
                StatusMessage = reason ?? "Your data cannot be deleted at this time.";
                IsSuccess = false;
                return RedirectToPage();
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

                StatusMessage = $"Your data has been permanently deleted. {totalDeleted} records were removed from the system.";
                IsSuccess = true;

                // Sign out the user since their account has been deleted
                return RedirectToPage("/Account/Logout", new { area = "" });
            }
            else
            {
                _logger.LogWarning("Failed to delete data for user {UserId}: {ErrorCode} - {ErrorMessage}",
                    user.Id, result.ErrorCode, result.ErrorMessage);

                StatusMessage = result.ErrorCode switch
                {
                    UserPurgeResultDto.UserNotFound => "User not found in the database.",
                    UserPurgeResultDto.UserHasAdminRole => "Cannot delete data for users with admin roles. Contact support.",
                    UserPurgeResultDto.DatabaseError => "A database error occurred. Please try again.",
                    UserPurgeResultDto.TransactionFailed => "Failed to delete data. Please try again.",
                    _ => result.ErrorMessage ?? "Failed to delete data. Please try again."
                };
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting data for user {UserId}", user.Id);
            StatusMessage = "An error occurred while deleting your data. Please try again.";
            IsSuccess = false;
        }

        return RedirectToPage();
    }
}
