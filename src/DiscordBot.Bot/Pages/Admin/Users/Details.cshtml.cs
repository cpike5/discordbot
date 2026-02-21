using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace DiscordBot.Bot.Pages.Admin.Users;

/// <summary>
/// Page model for displaying detailed user information and activity history.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class DetailsModel : PageModel
{
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        IUserManagementService userManagementService,
        ILogger<DetailsModel> logger)
    {
        _userManagementService = userManagementService;
        _logger = logger;
    }

    public UserDetailViewModel ViewModel { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        _logger.LogInformation("User {CurrentUserId} viewing details for user {TargetUserId}",
            currentUserId, id);

        var user = await _userManagementService.GetUserByIdAsync(id);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", id);
            return NotFound();
        }

        // Get recent activity for this user
        var activityLog = await _userManagementService.GetActivityLogAsync(id, page: 1, pageSize: 20);

        // Check permissions
        var canManage = await _userManagementService.CanManageUserAsync(currentUserId, id);
        var isSelf = currentUserId == id;

        ViewModel = new UserDetailViewModel
        {
            User = user,
            RecentActivity = activityLog.Items,
            CanEdit = canManage,
            CanDelete = canManage && !isSelf,
            CanResetPassword = canManage && !isSelf,
            CanChangeRole = canManage && !isSelf,
            CanUnlinkDiscord = canManage && user.IsDiscordLinked,
            IsSelf = isSelf
        };

        return Page();
    }
}
