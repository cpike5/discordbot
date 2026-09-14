using System.Security.Claims;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DiscordBot.Bot.Blazor.Pages.Admin.Users;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Admin/Users/Details.cshtml</c> +
/// <c>DetailsModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4a). Faithful,
/// read-only port: same <see cref="IUserManagementService.GetUserByIdAsync"/> +
/// <see cref="IUserManagementService.GetActivityLogAsync"/> + <see cref="IUserManagementService.CanManageUserAsync"/>
/// calls and the same permission-flag computation as <c>DetailsModel.OnGetAsync</c>.
/// </summary>
public partial class Details : ComponentBase
{
    [SupplyParameterFromQuery(Name = "id")]
    [Parameter]
    public string? Id { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private IUserManagementService UserManagementService { get; set; } = default!;

    [Inject]
    private ILogger<Details> Logger { get; set; } = default!;

    protected UserDto? User { get; private set; }
    protected IReadOnlyList<UserActivityLogDto> RecentActivity { get; private set; } = [];
    protected bool CanEdit { get; private set; }
    protected bool IsLoading { get; private set; } = true;
    protected bool UserNotFound { get; private set; }

    protected override async Task OnInitializedAsync() => await LoadAsync();

    protected override async Task OnParametersSetAsync()
    {
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
        var currentUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            UserNotFound = true;
            IsLoading = false;
            return;
        }

        Logger.LogInformation("User {CurrentUserId} viewing details for user {TargetUserId}", currentUserId, Id);

        var user = await UserManagementService.GetUserByIdAsync(Id);
        if (user is null)
        {
            Logger.LogWarning("User {UserId} not found", Id);
            UserNotFound = true;
            IsLoading = false;
            return;
        }

        var activityLog = await UserManagementService.GetActivityLogAsync(Id, page: 1, pageSize: 20);
        CanEdit = await UserManagementService.CanManageUserAsync(currentUserId, Id);

        User = user;
        RecentActivity = activityLog.Items;
        IsLoading = false;
    }

    private async Task<ClaimsPrincipal> GetUserAsync()
        => AuthenticationStateTask is not null
            ? (await AuthenticationStateTask).User
            : new ClaimsPrincipal(new ClaimsIdentity());

    protected static string UserEditUrl(string id) => $"/Admin/Users/Edit?id={Uri.EscapeDataString(id)}";

    protected static BadgeVariant RoleBadgeVariant(string role) => role switch
    {
        "SuperAdmin" => BadgeVariant.Error,
        "Admin" => BadgeVariant.Orange,
        "Moderator" => BadgeVariant.Blue,
        "Viewer" => BadgeVariant.Info,
        _ => BadgeVariant.Default
    };

    protected static (string Text, BadgeVariant Variant) ActivityBadge(UserActivityAction action) => action switch
    {
        UserActivityAction.UserCreated => ("Created", BadgeVariant.Success),
        UserActivityAction.UserUpdated => ("Updated", BadgeVariant.Info),
        UserActivityAction.UserEnabled => ("Enabled", BadgeVariant.Success),
        UserActivityAction.UserDisabled => ("Disabled", BadgeVariant.Warning),
        UserActivityAction.RoleAssigned => ("Role Assigned", BadgeVariant.Blue),
        UserActivityAction.RoleRemoved => ("Role Removed", BadgeVariant.Warning),
        UserActivityAction.PasswordReset => ("Password Reset", BadgeVariant.Orange),
        UserActivityAction.DiscordLinked => ("Discord Linked", BadgeVariant.Blue),
        UserActivityAction.DiscordUnlinked => ("Discord Unlinked", BadgeVariant.Warning),
        _ => (action.ToString(), BadgeVariant.Default)
    };
}
