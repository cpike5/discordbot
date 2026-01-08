using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Admin.Privacy;

/// <summary>
/// Page model for user data purge operations.
/// Provides functionality for privacy compliance and "right to be forgotten" requests.
/// Only accessible to SuperAdmins due to the sensitive and irreversible nature of data purges.
/// </summary>
[Authorize(Policy = "RequireSuperAdmin")]
public class IndexModel : PageModel
{
    private readonly IUserDataPurgeService _userDataPurgeService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IUserDataPurgeService userDataPurgeService,
        ILogger<IndexModel> logger)
    {
        _userDataPurgeService = userDataPurgeService;
        _logger = logger;
    }

    /// <summary>
    /// The Discord User ID to search for or purge.
    /// </summary>
    [BindProperty]
    public ulong DiscordUserId { get; set; }

    /// <summary>
    /// Optional reason for the purge operation (for audit logging).
    /// </summary>
    [BindProperty]
    public string? Reason { get; set; }

    /// <summary>
    /// Summary of user data (populated after preview).
    /// </summary>
    public UserDataSummary? Summary { get; set; }

    /// <summary>
    /// Success message displayed after purge operation (survives redirect via TempData).
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Error message displayed when operation fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Handles GET request - displays empty search form.
    /// </summary>
    public void OnGet()
    {
        // Empty page load - user will enter Discord User ID to preview
    }

    /// <summary>
    /// Handles POST request to preview user data before purging.
    /// Retrieves a summary of all data associated with the Discord User ID.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Page result with populated Summary.</returns>
    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        if (DiscordUserId == 0)
        {
            ErrorMessage = "Please enter a valid Discord User ID.";
            return Page();
        }

        try
        {
            _logger.LogInformation(
                "User {CurrentUser} previewing data for Discord user ID: {DiscordUserId}",
                User.Identity?.Name ?? "Unknown",
                DiscordUserId);

            Summary = await _userDataPurgeService.GetUserDataSummaryAsync(DiscordUserId, cancellationToken);

            if (!Summary.UserExists)
            {
                ErrorMessage = $"No user found with Discord ID {DiscordUserId}.";
                Summary = null;
                return Page();
            }

            if (Summary.TotalRecords == 0)
            {
                ErrorMessage = $"User {DiscordUserId} exists but has no associated data to purge.";
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing user data for Discord user ID: {DiscordUserId}", DiscordUserId);
            ErrorMessage = "An error occurred while retrieving user data. Please check the logs for details.";
            return Page();
        }
    }

    /// <summary>
    /// Handles POST request to permanently purge all user data.
    /// This operation is irreversible and should only be used for privacy compliance requests.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the same page with success message on success, or page redisplay with error on failure.</returns>
    public async Task<IActionResult> OnPostPurgeAsync(CancellationToken cancellationToken)
    {
        if (DiscordUserId == 0)
        {
            ErrorMessage = "Please enter a valid Discord User ID.";
            return Page();
        }

        // Get initiator from claims
        var initiatedBy = User.Identity?.Name ?? "Unknown";

        try
        {
            _logger.LogWarning(
                "User {CurrentUser} initiating data purge for Discord user ID: {DiscordUserId}, reason: {Reason}",
                initiatedBy,
                DiscordUserId,
                Reason ?? "Not specified");

            var result = await _userDataPurgeService.PurgeUserDataAsync(
                DiscordUserId,
                initiatedBy,
                Reason,
                cancellationToken);

            if (result.Succeeded)
            {
                _logger.LogWarning(
                    "Successfully purged data for Discord user ID: {DiscordUserId}. Total records deleted: {TotalRecords}",
                    DiscordUserId,
                    result.DeletedCounts?.TotalRecords ?? 0);

                SuccessMessage = $"Successfully purged all data for user {DiscordUserId}. " +
                                $"Total records deleted: {result.DeletedCounts?.TotalRecords ?? 0}.";

                // Redirect to clear form and show success message
                return RedirectToPage();
            }
            else
            {
                _logger.LogError(
                    "Failed to purge data for Discord user ID: {DiscordUserId}. Error: {ErrorCode} - {ErrorMessage}",
                    DiscordUserId,
                    result.ErrorCode,
                    result.ErrorMessage);

                ErrorMessage = result.ErrorMessage ?? "An unknown error occurred during the purge operation.";

                // Reload the summary to show current state
                try
                {
                    Summary = await _userDataPurgeService.GetUserDataSummaryAsync(DiscordUserId, cancellationToken);
                }
                catch (Exception summaryEx)
                {
                    _logger.LogError(summaryEx, "Error reloading summary after failed purge");
                }

                return Page();
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("User data purge operation cancelled for Discord user ID: {DiscordUserId}", DiscordUserId);
            ErrorMessage = "The purge operation was cancelled.";
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during purge operation for Discord user ID: {DiscordUserId}", DiscordUserId);
            ErrorMessage = "An unexpected error occurred during the purge operation. Please check the logs for details.";
            return Page();
        }
    }
}
