using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DiscordBot.Bot.Pages.Admin.Users;

/// <summary>
/// Page model for listing and managing users.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class IndexModel : PaginatedPageModel
{
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IUserManagementService userManagementService,
        ILogger<IndexModel> logger)
    {
        _userManagementService = userManagementService;
        _logger = logger;

        PageSize = 20;
    }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? RoleFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? ActiveFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? DiscordLinkedFilter { get; set; }

    public UserListViewModel ViewModel { get; set; } = new();

    /// <summary>True when any search term or filter is applied, so an empty list means "nothing matches".</summary>
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchTerm)
        || !string.IsNullOrEmpty(RoleFilter)
        || ActiveFilter.HasValue
        || DiscordLinkedFilter.HasValue;

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

    public async Task<IActionResult> OnPostToggleActiveAsync(string userId, bool isActive, string? returnUrl = null)
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
            TempData.SetSuccessToast(isActive ? "User enabled. They can sign in again." : "User disabled. They can no longer sign in.");
            _logger.LogInformation("User {UserId} {Action} user {TargetUserId}",
                currentUserId, isActive ? "enabled" : "disabled", userId);
        }
        else
        {
            TempData.SetErrorToast(result.ErrorMessage ?? "Failed to update user status");
            _logger.LogWarning("Failed to toggle active status for user {UserId}: {Error}",
                userId, result.ErrorMessage);
        }

        // Back to the same filtered, paged list the admin was looking at
        return LocalRedirect(ReturnUrlHelper.Sanitize(returnUrl, Url.Page("Index") ?? "/Admin/Users"));
    }
}
