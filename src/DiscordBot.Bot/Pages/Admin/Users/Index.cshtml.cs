using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace DiscordBot.Bot.Pages.Admin.Users;

/// <summary>
/// Page model for listing and managing users.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class IndexModel : PageModel
{
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IUserManagementService userManagementService,
        ILogger<IndexModel> logger)
    {
        _userManagementService = userManagementService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RoleFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? ActiveFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? DiscordLinkedFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 20;

    public UserListViewModel ViewModel { get; set; } = new();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            _logger.LogWarning("User ID not found in claims");
            return Unauthorized();
        }

        _logger.LogInformation("User {UserId} accessing user management list", currentUserId);

        // Build search query
        var query = new UserSearchQueryDto
        {
            SearchTerm = SearchTerm,
            Role = RoleFilter,
            IsActive = ActiveFilter,
            IsDiscordLinked = DiscordLinkedFilter,
            Page = CurrentPage,
            PageSize = PageSize,
            SortBy = "CreatedAt",
            SortDescending = true
        };

        // Get users
        var paginatedUsers = await _userManagementService.GetUsersAsync(query);

        // Get available roles for filter dropdown
        var availableRoles = await _userManagementService.GetAvailableRolesAsync(currentUserId);

        // Build view model
        ViewModel = new UserListViewModel
        {
            Users = paginatedUsers.Items,
            TotalCount = paginatedUsers.TotalCount,
            CurrentPage = paginatedUsers.Page,
            PageSize = paginatedUsers.PageSize,
            TotalPages = paginatedUsers.TotalPages,
            SearchTerm = SearchTerm,
            RoleFilter = RoleFilter,
            ActiveFilter = ActiveFilter,
            DiscordLinkedFilter = DiscordLinkedFilter,
            AvailableRoles = availableRoles,
            CurrentUserId = currentUserId,
            CanCreateUsers = User.IsInRole("Admin") || User.IsInRole("SuperAdmin")
        };

        return Page();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(string userId, bool isActive)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _userManagementService.SetUserActiveStatusAsync(
            userId,
            isActive,
            currentUserId,
            ipAddress);

        if (result.Succeeded)
        {
            SuccessMessage = $"User {(isActive ? "enabled" : "disabled")} successfully";
            _logger.LogInformation("User {UserId} {Action} user {TargetUserId}",
                currentUserId, isActive ? "enabled" : "disabled", userId);
        }
        else
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to update user status";
            _logger.LogWarning("Failed to toggle active status for user {UserId}: {Error}",
                userId, result.ErrorMessage);
        }

        return RedirectToPage();
    }
}
