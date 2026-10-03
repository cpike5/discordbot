using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using System.Security.Claims;

namespace DiscordBot.Bot.Pages.Admin;

/// <summary>
/// Page model for purging user data for GDPR compliance.
/// <para>
/// The preview is a GET. The purge is a POST that always redirects (post/redirect/get), so a
/// refresh afterwards loads the page and never runs the purge again. The outcome travels in
/// TempData and shows once.
/// </para>
/// </summary>
[Authorize(Policy = "RequireSuperAdmin")]
public class UserPurgeModel : PageModel
{
    private const string ResultTempDataKey = "UserPurgeResult";

    private readonly IUserPurgeService _purgeService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserPurgeModel> _logger;

    public UserPurgeModel(
        IUserPurgeService purgeService,
        IUserRepository userRepository,
        ILogger<UserPurgeModel> logger)
    {
        _purgeService = purgeService;
        _userRepository = userRepository;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? DiscordUserId { get; set; }

    public UserPurgeResultDto? PreviewResult { get; set; }
    public string? CannotPurgeReason { get; set; }
    public bool ShowPreview { get; set; }

    /// <summary>The Discord username of the previewed user, or null when the bot has no record of one.</summary>
    public string? DiscordUsername { get; set; }

    /// <summary>The outcome of the purge the user just ran, shown once after the redirect.</summary>
    public UserPurgeOutcome? Outcome { get; set; }

    /// <summary>
    /// Page-state error shown when the preview for the requested user could not be loaded.
    /// Action results from the purge POST are shown as toasts instead.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>The summary of a finished purge, as it travels through TempData.</summary>
    public sealed record UserPurgeOutcome(
        string DiscordUserId,
        string? Username,
        Dictionary<string, int> DeletedCounts,
        string? CorrelationId);

    public async Task<IActionResult> OnGetAsync()
    {
        Outcome = TakeOutcome();

        if (string.IsNullOrEmpty(DiscordUserId))
        {
            return Page();
        }

        if (!ulong.TryParse(DiscordUserId.Trim(), out var userId))
        {
            ModelState.AddModelError(nameof(DiscordUserId), "The user ID must be a number, such as 123456789012345678.");
            return Page();
        }

        // Check if user can be purged
        var (canPurge, reason) = await _purgeService.CanPurgeUserAsync(userId);
        if (!canPurge)
        {
            CannotPurgeReason = reason;
            return Page();
        }

        // Get preview of data to be deleted
        PreviewResult = await _purgeService.PreviewPurgeAsync(userId);

        if (PreviewResult.Success)
        {
            ShowPreview = true;
            DiscordUsername = await FindUsernameAsync(userId);
        }
        else
        {
            ErrorMessage = PreviewResult.ErrorMessage ?? "The preview could not be generated.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(DiscordUserId) || !ulong.TryParse(DiscordUserId.Trim(), out var userId))
        {
            ModelState.AddModelError(nameof(DiscordUserId), "The user ID must be a number, such as 123456789012345678.");
            return Page();
        }

        // Check if user can be purged
        var (canPurge, reason) = await _purgeService.CanPurgeUserAsync(userId);
        if (!canPurge)
        {
            TempData.SetErrorToast(reason ?? "This user cannot be purged.");
            return RedirectToPage(new { DiscordUserId });
        }

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // The user row is deleted by the purge, so read the name now for the result message.
        var username = await FindUsernameAsync(userId);

        _logger.LogInformation(
            "Admin {AdminId} initiating purge for Discord user {DiscordUserId}",
            adminUserId, userId);

        var result = await _purgeService.PurgeUserDataAsync(
            userId,
            PurgeInitiator.Admin,
            adminUserId);

        if (result.Success)
        {
            _logger.LogInformation(
                "Successfully purged data for Discord user {DiscordUserId}. Total records: {TotalDeleted}",
                userId, result.DeletedCounts.Values.Sum());

            TempData[ResultTempDataKey] = JsonSerializer.Serialize(new UserPurgeOutcome(
                userId.ToString(), username, result.DeletedCounts, result.AuditLogCorrelationId));

            // Post/redirect/get, without the ID: the user is gone, and a refresh must not retry.
            return RedirectToPage();
        }

        // A failed transaction's message carries exception text; it is logged below, not shown.
        TempData.SetErrorToast(result.ErrorCode == UserPurgeResultDto.TransactionFailed
            ? "An error occurred during the purge. Check the logs before you try again."
            : result.ErrorMessage ?? "An error occurred during the purge.");

        _logger.LogError(
            "Failed to purge data for Discord user {DiscordUserId}: {Error}",
            userId, result.ErrorMessage);

        return RedirectToPage(new { DiscordUserId });
    }

    private async Task<string?> FindUsernameAsync(ulong userId)
    {
        try
        {
            var user = await _userRepository.GetByDiscordIdAsync(userId);
            return string.IsNullOrWhiteSpace(user?.Username) ? null : user.Username;
        }
        catch (Exception ex)
        {
            // The name is only a convenience in the confirmation; the purge does not need it.
            _logger.LogWarning(ex, "Could not look up the username for Discord user {DiscordUserId}", userId);
            return null;
        }
    }

    private UserPurgeOutcome? TakeOutcome()
    {
        if (TempData[ResultTempDataKey] is not string json)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<UserPurgeOutcome>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
