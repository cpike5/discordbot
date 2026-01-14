using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using System.Security.Claims;

namespace DiscordBot.Bot.Pages.Admin;

/// <summary>
/// Page model for purging user data for GDPR compliance.
/// </summary>
[Authorize(Policy = "RequireSuperAdmin")]
public class UserPurgeModel : PageModel
{
    private readonly IUserPurgeService _purgeService;
    private readonly ILogger<UserPurgeModel> _logger;

    public UserPurgeModel(IUserPurgeService purgeService, ILogger<UserPurgeModel> logger)
    {
        _purgeService = purgeService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? DiscordUserId { get; set; }

    public UserPurgeResultDto? PreviewResult { get; set; }
    public UserPurgeResultDto? PurgeResult { get; set; }
    public string? CannotPurgeReason { get; set; }
    public bool ShowPreview { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (string.IsNullOrEmpty(DiscordUserId))
        {
            return Page();
        }

        if (!ulong.TryParse(DiscordUserId, out var userId))
        {
            ModelState.AddModelError(nameof(DiscordUserId), "Invalid Discord User ID format.");
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
        }
        else
        {
            ErrorMessage = PreviewResult.ErrorMessage ?? "Failed to generate preview";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(DiscordUserId) || !ulong.TryParse(DiscordUserId, out var userId))
        {
            ModelState.AddModelError(nameof(DiscordUserId), "Invalid Discord User ID format.");
            return Page();
        }

        // Check if user can be purged
        var (canPurge, reason) = await _purgeService.CanPurgeUserAsync(userId);
        if (!canPurge)
        {
            CannotPurgeReason = reason;
            ErrorMessage = reason;
            return Page();
        }

        var adminUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        _logger.LogInformation(
            "Admin {AdminId} initiating purge for Discord user {DiscordUserId}",
            adminUserId, userId);

        PurgeResult = await _purgeService.PurgeUserDataAsync(
            userId,
            PurgeInitiator.Admin,
            adminUserId);

        if (PurgeResult.Success)
        {
            var totalDeleted = PurgeResult.DeletedCounts.Values.Sum();
            SuccessMessage = $"User data purged successfully. {totalDeleted} records deleted.";

            _logger.LogInformation(
                "Successfully purged data for Discord user {DiscordUserId}. Total records: {TotalDeleted}",
                userId, totalDeleted);
        }
        else
        {
            ErrorMessage = PurgeResult.ErrorMessage ?? "An error occurred during purge.";

            _logger.LogError(
                "Failed to purge data for Discord user {DiscordUserId}: {Error}",
                userId, PurgeResult.ErrorMessage);
        }

        return Page();
    }
}
