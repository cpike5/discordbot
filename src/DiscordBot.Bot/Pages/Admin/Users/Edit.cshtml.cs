using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace DiscordBot.Bot.Pages.Admin.Users;

/// <summary>
/// Page model for editing an existing user.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class EditModel : PageModel
{
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<EditModel> _logger;

    public EditModel(
        IUserManagementService userManagementService,
        ILogger<EditModel> logger)
    {
        _userManagementService = userManagementService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public UserFormViewModel ViewModel { get; set; } = new();

    public string? ErrorMessage { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public class InputModel
    {
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        [Required(ErrorMessage = "Role is required")]
        public string Role { get; set; } = "Viewer";

        public bool IsActive { get; set; } = true;
    }

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

        var user = await _userManagementService.GetUserByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        Input = new InputModel
        {
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.HighestRole,
            IsActive = user.IsActive
        };

        await LoadViewModelAsync(currentUserId, user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            _logger.LogWarning("OnPostAsync: Unauthorized - no current user ID");
            return Unauthorized();
        }

        _logger.LogInformation("OnPostAsync: Received form data - UserId={UserId}, Email={Email}, DisplayName={DisplayName}, Role={Role}, IsActive={IsActive}",
            Input.UserId, Input.Email, Input.DisplayName, Input.Role, Input.IsActive);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("OnPostAsync: ModelState is invalid. Errors: {Errors}",
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            var user = await _userManagementService.GetUserByIdAsync(Input.UserId);
            if (user == null)
            {
                return NotFound();
            }
            await LoadViewModelAsync(currentUserId, user);
            return Page();
        }

        _logger.LogInformation("Updating user {UserId} by user {ActorUserId}",
            Input.UserId, currentUserId);

        var updateDto = new UserUpdateDto
        {
            Email = Input.Email,
            DisplayName = Input.DisplayName,
            Role = Input.Role,
            IsActive = Input.IsActive
        };

        _logger.LogInformation("OnPostAsync: Created UpdateDto - Email={Email}, DisplayName={DisplayName}, Role={Role}, IsActive={IsActive}",
            updateDto.Email, updateDto.DisplayName, updateDto.Role, updateDto.IsActive);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _userManagementService.UpdateUserAsync(Input.UserId, updateDto, currentUserId, ipAddress);

        if (result.Succeeded)
        {
            _logger.LogInformation("Successfully updated user {UserId}", Input.UserId);
            SuccessMessage = "User updated successfully";
            return RedirectToPage("Edit", new { id = Input.UserId });
        }

        _logger.LogWarning("Failed to update user {UserId}: {Error}", Input.UserId, result.ErrorMessage);
        ErrorMessage = result.ErrorMessage ?? "Failed to update user";

        var userForViewModel = await _userManagementService.GetUserByIdAsync(Input.UserId);
        if (userForViewModel != null)
        {
            await LoadViewModelAsync(currentUserId, userForViewModel);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(string userId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        _logger.LogInformation("Resetting password for user {UserId} by user {ActorUserId}",
            userId, currentUserId);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _userManagementService.ResetPasswordAsync(userId, currentUserId, ipAddress);

        if (result.Succeeded && !string.IsNullOrEmpty(result.GeneratedPassword))
        {
            _logger.LogInformation("Successfully reset password for user {UserId}", userId);
            SuccessMessage = $"Password reset successfully. New temporary password: {result.GeneratedPassword}";
        }
        else
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to reset password";
            _logger.LogWarning("Failed to reset password for user {UserId}: {Error}",
                userId, result.ErrorMessage);
        }

        return RedirectToPage("Edit", new { id = userId });
    }

    public async Task<IActionResult> OnPostUnlinkDiscordAsync(string userId)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        _logger.LogInformation("Unlinking Discord for user {UserId} by user {ActorUserId}",
            userId, currentUserId);

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _userManagementService.UnlinkDiscordAccountAsync(userId, currentUserId, ipAddress);

        if (result.Succeeded)
        {
            _logger.LogInformation("Successfully unlinked Discord for user {UserId}", userId);
            SuccessMessage = "Discord account unlinked successfully";
        }
        else
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to unlink Discord account";
            _logger.LogWarning("Failed to unlink Discord for user {UserId}: {Error}",
                userId, result.ErrorMessage);
        }

        return RedirectToPage("Edit", new { id = userId });
    }

    private async Task LoadViewModelAsync(string currentUserId, UserDto user)
    {
        var availableRoles = await _userManagementService.GetAvailableRolesAsync(currentUserId);
        var isSelf = currentUserId == user.Id;

        ViewModel = new UserFormViewModel
        {
            UserId = user.Id,
            Email = Input.Email,
            DisplayName = Input.DisplayName,
            Role = Input.Role,
            IsActive = Input.IsActive,
            AvailableRoles = availableRoles.Select(r => new SelectListItem
            {
                Value = r,
                Text = r,
                Selected = r == Input.Role
            }).ToList(),
            IsDiscordLinked = user.IsDiscordLinked,
            DiscordUsername = user.DiscordUsername,
            DiscordAvatarUrl = user.DiscordAvatarUrl,
            CanChangeRole = !isSelf,
            CanChangeActiveStatus = !isSelf,
            IsSelf = isSelf
        };
    }
}
