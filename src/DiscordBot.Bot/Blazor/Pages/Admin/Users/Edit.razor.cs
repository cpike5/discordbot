using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DiscordBot.Bot.Blazor.Pages.Admin.Users;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Admin/Users/Edit.cshtml</c> +
/// <c>EditModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4a). Reproduces
/// <c>EditModel</c>'s save/reset-password/unlink-Discord handlers as component methods called
/// from two <see cref="ConfirmModal"/>s instead of separate POST handlers, and the same
/// <c>IsSelf</c>-gated role/active-status restrictions.
/// </summary>
/// <remarks>
/// A save reloads the user from the service and stays on the page (matching
/// <c>EditModel.OnPostAsync</c>'s <c>RedirectToPage("Edit", new { id })</c> - a redirect back to
/// itself is a same-page reload in a Razor Page, so staying and reloading the model is the
/// faithful port here, not a client-side navigation). An unresolvable <see cref="Id"/> is a
/// 200 response with an empty-state body, not a 404 - see the file-level "deviation" note this
/// cluster's task instructions call out; a real HTTP status code is not settable once the
/// interactive circuit is already serving the page.
/// </remarks>
public partial class Edit : ComponentBase
{
    [SupplyParameterFromQuery(Name = "id")]
    [Parameter]
    public string? Id { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private IUserManagementService UserManagementService { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private CircuitClientInfoService CircuitInfo { get; set; } = default!;

    [Inject]
    private BrowserInterop BrowserInterop { get; set; } = default!;

    [Inject]
    private ILogger<Edit> Logger { get; set; } = default!;

    protected InputModel Input { get; set; } = new();
    protected UserDto? User { get; private set; }
    protected bool UserNotFound { get; private set; }
    protected bool IsLoading { get; private set; } = true;
    protected bool IsSelf { get; private set; }
    protected string? ErrorMessage { get; set; }
    protected string? GeneratedPassword { get; set; }
    protected IReadOnlyList<string> AvailableRoles { get; private set; } = [];

    protected List<SelectOption> RoleOptions => AvailableRoles.Select(r => new SelectOption { Value = r, Text = r }).ToList();

    /// <summary>
    /// True when <see cref="IUserManagementService.CanManageUserAsync"/> allows
    /// <c>_currentUserId</c> to manage <see cref="User"/> - always <see langword="false"/> for
    /// self (that method restricts self-management by design) and for a target whose highest role
    /// outranks the actor's (e.g. an Admin against a SuperAdmin). Distinct from
    /// <see cref="CanAccessEdit"/>: this page still lets a user edit their own basic profile
    /// fields even though they can never "manage" themselves by this method's definition.
    /// </summary>
    private bool _targetManageable;

    /// <summary>
    /// Gates whether this page renders the edit form at all: either editing yourself (basic
    /// profile fields only - see <see cref="IsSelf"/>'s existing Role/Active-status restriction),
    /// or the actor has been granted management rights over this specific target via
    /// <see cref="IUserManagementService.CanManageUserAsync"/>. <see langword="false"/> closes the
    /// gap the legacy <c>EditModel</c> left open: it rendered the same form regardless of
    /// <c>CanManageUserAsync</c>, so an Admin could reach <c>/Admin/Users/Edit?id=&lt;anySuperAdmin&gt;</c>
    /// directly (the link was merely hidden on <c>Details.razor</c>'s <c>CanEdit</c>) and edit,
    /// reset the password of, or unlink Discord for a user outside their privilege tier.
    /// </summary>
    protected bool CanAccessEdit => IsSelf || _targetManageable;

    /// <summary>
    /// Restores the deleted <c>UserDetailViewModel.CanChangeRole</c> semantics
    /// (<c>canManage &amp;&amp; !isSelf</c> - see git history, commit 2b0646a). Functionally
    /// equivalent to <c>!IsSelf</c> whenever the form is actually visible (<see cref="CanAccessEdit"/>
    /// already requires <see cref="_targetManageable"/> for a non-self target), kept as the full
    /// formula for clarity and as defense in depth.
    /// </summary>
    protected bool CanChangeRole => _targetManageable && !IsSelf;

    /// <summary>Restores <c>UserDetailViewModel.CanResetPassword</c> (<c>canManage &amp;&amp; !isSelf</c>) - hides/refuses password reset for your own account via this admin page, same as an Admin cannot reset a SuperAdmin's.</summary>
    protected bool CanResetPassword => _targetManageable && !IsSelf;

    /// <summary>Restores <c>UserDetailViewModel.CanUnlinkDiscord</c> (<c>canManage &amp;&amp; user.IsDiscordLinked</c>) - no <c>!IsSelf</c> term in the original either, so this is false for self by the same <c>_targetManageable</c> rule.</summary>
    protected bool CanUnlinkDiscord => _targetManageable && User is { IsDiscordLinked: true };

    private ConfirmModal? _resetPasswordModal;
    private ConfirmModal? _unlinkDiscordModal;
    private string? _currentUserId;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    protected override async Task OnParametersSetAsync()
    {
        // SupplyParameterFromQuery re-runs OnParametersSetAsync on every navigation, including
        // one this page triggers itself (there is none here - saves stay in place) and one a
        // caller navigating straight to a different ?id= would trigger.
        if (User is not null && !string.Equals(User.Id, Id, StringComparison.Ordinal))
        {
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        UserNotFound = false;

        if (string.IsNullOrEmpty(Id))
        {
            UserNotFound = true;
            IsLoading = false;
            return;
        }

        var principal = await GetUserAsync();
        _currentUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(_currentUserId))
        {
            UserNotFound = true;
            IsLoading = false;
            return;
        }

        var user = await UserManagementService.GetUserByIdAsync(Id);
        if (user is null)
        {
            UserNotFound = true;
            IsLoading = false;
            return;
        }

        User = user;
        IsSelf = _currentUserId == user.Id;
        _targetManageable = await UserManagementService.CanManageUserAsync(_currentUserId, user.Id);
        Input = new InputModel
        {
            UserId = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.HighestRole,
            IsActive = user.IsActive
        };

        AvailableRoles = await UserManagementService.GetAvailableRolesAsync(_currentUserId);
        IsLoading = false;
    }

    protected async Task HandleValidSubmit()
    {
        if (string.IsNullOrEmpty(_currentUserId) || !CanAccessEdit)
        {
            return;
        }

        var updateDto = new UserUpdateDto
        {
            Email = Input.Email,
            DisplayName = Input.DisplayName,
            Role = Input.Role,
            IsActive = Input.IsActive
        };

        var result = await UserManagementService.UpdateUserAsync(Input.UserId, updateDto, _currentUserId, CircuitInfo.RemoteIp?.ToString());

        if (result.Succeeded)
        {
            Logger.LogInformation("Successfully updated user {UserId}", Input.UserId);
            ErrorMessage = null;
            Toast.Success("User updated successfully");
            await LoadAsync();
        }
        else
        {
            Logger.LogWarning("Failed to update user {UserId}: {Error}", Input.UserId, result.ErrorMessage);
            ErrorMessage = result.ErrorMessage ?? "Failed to update user";
        }
    }

    protected async Task RequestResetPassword()
    {
        if (!CanResetPassword)
        {
            return;
        }

        if (_resetPasswordModal is not null && await _resetPasswordModal.ShowAsync())
        {
            await ConfirmResetPasswordAsync();
        }
    }

    private async Task ConfirmResetPasswordAsync()
    {
        if (string.IsNullOrEmpty(_currentUserId) || User is null || !CanResetPassword)
        {
            return;
        }

        var result = await UserManagementService.ResetPasswordAsync(User.Id, _currentUserId, CircuitInfo.RemoteIp?.ToString());

        if (result.Succeeded && !string.IsNullOrEmpty(result.GeneratedPassword))
        {
            Logger.LogInformation("Successfully reset password for user {UserId}", User.Id);
            GeneratedPassword = result.GeneratedPassword;
            ErrorMessage = null;
            await LoadAsync();
        }
        else
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to reset password";
            Logger.LogWarning("Failed to reset password for user {UserId}: {Error}", User.Id, result.ErrorMessage);
        }
    }

    protected async Task RequestUnlinkDiscord()
    {
        if (!CanUnlinkDiscord)
        {
            return;
        }

        if (_unlinkDiscordModal is not null && await _unlinkDiscordModal.ShowAsync())
        {
            await ConfirmUnlinkDiscordAsync();
        }
    }

    private async Task ConfirmUnlinkDiscordAsync()
    {
        if (string.IsNullOrEmpty(_currentUserId) || User is null || !CanUnlinkDiscord)
        {
            return;
        }

        var result = await UserManagementService.UnlinkDiscordAccountAsync(User.Id, _currentUserId, CircuitInfo.RemoteIp?.ToString());

        if (result.Succeeded)
        {
            Logger.LogInformation("Successfully unlinked Discord for user {UserId}", User.Id);
            ErrorMessage = null;
            Toast.Success("Discord account unlinked successfully");
            await LoadAsync();
        }
        else
        {
            ErrorMessage = result.ErrorMessage ?? "Failed to unlink Discord account";
            Logger.LogWarning("Failed to unlink Discord for user {UserId}: {Error}", User.Id, result.ErrorMessage);
        }
    }

    protected async Task CopyGeneratedPasswordAsync()
    {
        if (!string.IsNullOrEmpty(GeneratedPassword))
        {
            await BrowserInterop.CopyToClipboardAsync(GeneratedPassword);
            Toast.Success("Password copied to clipboard");
        }
    }

    protected void DismissGeneratedPassword() => GeneratedPassword = null;

    private async Task<ClaimsPrincipal> GetUserAsync()
        => AuthenticationStateTask is not null
            ? (await AuthenticationStateTask).User
            : new ClaimsPrincipal(new ClaimsIdentity());

    protected static string UserDetailsUrl(string id) => $"/Admin/Users/Details?id={Uri.EscapeDataString(id)}";

    /// <summary>Mirrors <c>EditModel.InputModel</c>.</summary>
    public sealed class InputModel
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
}
