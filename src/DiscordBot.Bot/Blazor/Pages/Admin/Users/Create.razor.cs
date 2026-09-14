using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DiscordBot.Bot.Blazor.Pages.Admin.Users;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Admin/Users/Create.cshtml</c> +
/// <c>CreateModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4a). <see cref="InputModel"/>
/// keeps the same data annotations the legacy page's nested <c>InputModel</c> used, now validated
/// by <c>&lt;DataAnnotationsValidator&gt;</c> inside an <c>EditForm</c> instead of ASP.NET Core
/// model binding + jQuery unobtrusive validation.
/// </summary>
public partial class Create : ComponentBase
{
    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private IUserManagementService UserManagementService { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private CircuitClientInfoService CircuitInfo { get; set; } = default!;

    [Inject]
    private ILogger<Create> Logger { get; set; } = default!;

    protected InputModel Input { get; set; } = new();
    protected IReadOnlyList<string> AvailableRoles { get; private set; } = [];
    protected string? ErrorMessage { get; set; }

    protected List<SelectOption> RoleOptions => AvailableRoles.Select(r => new SelectOption { Value = r, Text = r }).ToList();

    private string? _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        var user = await GetUserAsync();
        _currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(_currentUserId))
        {
            AvailableRoles = await UserManagementService.GetAvailableRolesAsync(_currentUserId);
        }
    }

    protected async Task HandleValidSubmit()
    {
        if (string.IsNullOrEmpty(_currentUserId))
        {
            return;
        }

        Logger.LogInformation("Creating new user with email: {Email} by user: {UserId}", Input.Email, _currentUserId);

        var createDto = new UserCreateDto
        {
            Email = Input.Email,
            DisplayName = Input.DisplayName,
            Password = Input.Password,
            ConfirmPassword = Input.ConfirmPassword,
            Role = Input.Role,
            SendWelcomeEmail = Input.SendWelcomeEmail
        };

        var result = await UserManagementService.CreateUserAsync(createDto, _currentUserId, CircuitInfo.RemoteIp?.ToString());

        if (result.Succeeded)
        {
            Logger.LogInformation("Successfully created user: {Email}", Input.Email);
            Toast.Success($"User {Input.Email} created successfully");
            NavigationManager.NavigateTo("/Admin/Users");
            return;
        }

        Logger.LogWarning("Failed to create user {Email}: {Error}", Input.Email, result.ErrorMessage);
        ErrorMessage = result.ErrorMessage ?? "Failed to create user";
    }

    private async Task<ClaimsPrincipal> GetUserAsync()
        => AuthenticationStateTask is not null
            ? (await AuthenticationStateTask).User
            : new ClaimsPrincipal(new ClaimsIdentity());

    /// <summary>Mirrors <c>CreateModel.InputModel</c> exactly, including its data annotations.</summary>
    public sealed class InputModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = string.Empty;

        public string? DisplayName { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password confirmation is required")]
        [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Role is required")]
        public string Role { get; set; } = "Viewer";

        public bool SendWelcomeEmail { get; set; } = true;
    }
}
