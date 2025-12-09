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
/// Page model for creating a new user.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class CreateModel : PageModel
{
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        IUserManagementService userManagementService,
        ILogger<CreateModel> logger)
    {
        _userManagementService = userManagementService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public UserFormViewModel ViewModel { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password confirmation is required")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required")]
        public string Role { get; set; } = "Viewer";

        public bool SendWelcomeEmail { get; set; } = true;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        await LoadViewModelAsync(currentUserId);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            return Unauthorized();
        }

        if (!ModelState.IsValid)
        {
            await LoadViewModelAsync(currentUserId);
            return Page();
        }

        _logger.LogInformation("Creating new user with email: {Email} by user: {UserId}",
            Input.Email, currentUserId);

        var createDto = new UserCreateDto
        {
            Email = Input.Email,
            DisplayName = Input.DisplayName,
            Password = Input.Password,
            ConfirmPassword = Input.ConfirmPassword,
            Role = Input.Role,
            SendWelcomeEmail = Input.SendWelcomeEmail
        };

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _userManagementService.CreateUserAsync(createDto, currentUserId, ipAddress);

        if (result.Succeeded)
        {
            _logger.LogInformation("Successfully created user: {Email}", Input.Email);
            TempData["SuccessMessage"] = $"User {Input.Email} created successfully";
            return RedirectToPage("Index");
        }

        _logger.LogWarning("Failed to create user {Email}: {Error}", Input.Email, result.ErrorMessage);
        ErrorMessage = result.ErrorMessage ?? "Failed to create user";
        await LoadViewModelAsync(currentUserId);
        return Page();
    }

    private async Task LoadViewModelAsync(string currentUserId)
    {
        var availableRoles = await _userManagementService.GetAvailableRolesAsync(currentUserId);

        ViewModel = new UserFormViewModel
        {
            UserId = null,
            Email = Input.Email,
            DisplayName = Input.DisplayName,
            Role = Input.Role,
            IsActive = true,
            AvailableRoles = availableRoles.Select(r => new SelectListItem
            {
                Value = r,
                Text = r,
                Selected = r == Input.Role
            }).ToList(),
            CanChangeRole = true,
            CanChangeActiveStatus = false, // Not applicable for create
            IsSelf = false
        };
    }
}
