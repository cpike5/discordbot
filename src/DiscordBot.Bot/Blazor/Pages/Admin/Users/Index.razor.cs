using System.Security.Claims;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DiscordBot.Bot.Blazor.Pages.Admin.Users;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Admin/Users/Index.cshtml</c> +
/// <c>IndexModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4a). Reproduces
/// <c>IndexModel</c>'s query, sort ("CreatedAt" desc) and <c>CanCreateUsers</c> logic, plus a
/// real UI for the page model's previously-unreachable <c>OnPostToggleActiveAsync</c> handler
/// (the legacy markup never rendered a control for it - see the file header on
/// <see cref="HandleToggleConfirmed"/>).
/// </summary>
/// <remarks>
/// Resolves once per distinct query and persists the result across the prerender-to-circuit
/// boundary with <see cref="PersistentComponentState"/>, the same pattern
/// <c>Blazor/Pages/Search.razor.cs</c> uses - here keyed by the whole filter/page query instead
/// of a single search term.
/// </remarks>
public partial class Index : ComponentBase, IDisposable
{
    private const int PageSize = 20;

    [SupplyParameterFromQuery(Name = "SearchTerm")]
    [Parameter]
    public string? SearchTerm { get; set; }

    [SupplyParameterFromQuery(Name = "RoleFilter")]
    [Parameter]
    public string? RoleFilter { get; set; }

    [SupplyParameterFromQuery(Name = "ActiveFilter")]
    [Parameter]
    public bool? ActiveFilter { get; set; }

    [SupplyParameterFromQuery(Name = "DiscordLinkedFilter")]
    [Parameter]
    public bool? DiscordLinkedFilter { get; set; }

    [SupplyParameterFromQuery(Name = "pageNumber")]
    [Parameter]
    public int PageNumber { get; set; } = 1;

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
    private PersistentComponentState ApplicationState { get; set; } = default!;

    [Inject]
    private ILogger<Index> Logger { get; set; } = default!;

    protected IReadOnlyList<UserDto> Users { get; private set; } = [];
    protected int TotalCount { get; private set; }
    protected int CurrentPage { get; private set; } = 1;
    protected int TotalPages { get; private set; }
    protected IReadOnlyList<string> AvailableRoles { get; private set; } = [];
    protected bool CanCreateUsers { get; private set; }
    protected bool IsLoading { get; private set; } = true;

    /// <summary>Live text of the search box - seeded from <see cref="SearchTerm"/>, only applied to the query on submit.</summary>
    protected string? SearchTermInput { get; set; }
    protected string RoleFilterInput { get; set; } = "";
    protected string ActiveFilterInput { get; set; } = "";
    protected string DiscordLinkedFilterInput { get; set; } = "";

    private ConfirmModal? _toggleModal;
    private UserDto? _pendingToggleUser;
    private string? _currentUserId;
    private PersistingComponentStateSubscription _persistingSubscription;
    private (string? SearchTerm, string? RoleFilter, bool? ActiveFilter, bool? DiscordLinkedFilter, int PageNumber) _resolvedQuery;

    private string PersistenceKey => $"Admin.Users.Index.{SearchTerm}.{RoleFilter}.{ActiveFilter}.{DiscordLinkedFilter}.{PageNumber}";

    protected string ToggleModalMessage => _pendingToggleUser is null
        ? string.Empty
        : $"Are you sure you want to {(_pendingToggleUser.IsActive ? "disable" : "enable")} {_pendingToggleUser.Email}?";

    protected override async Task OnInitializedAsync()
    {
        _persistingSubscription = ApplicationState.RegisterOnPersisting(PersistCurrentResultAsync);
        SeedInputsFromQuery();

        if (ApplicationState.TryTakeFromJson<IndexPersistedState>(PersistenceKey, out var restored) && restored is not null)
        {
            Users = restored.Users;
            TotalCount = restored.TotalCount;
            CurrentPage = restored.CurrentPage;
            TotalPages = restored.TotalPages;
            AvailableRoles = restored.AvailableRoles;
            CanCreateUsers = restored.CanCreateUsers;
            _currentUserId = restored.CurrentUserId;
            _resolvedQuery = CurrentQuery;
            IsLoading = false;
        }
        else
        {
            await LoadAsync();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_resolvedQuery != CurrentQuery)
        {
            SeedInputsFromQuery();
            await LoadAsync();
        }
    }

    private (string?, string?, bool?, bool?, int) CurrentQuery => (SearchTerm, RoleFilter, ActiveFilter, DiscordLinkedFilter, PageNumber);

    private void SeedInputsFromQuery()
    {
        SearchTermInput = SearchTerm;
        RoleFilterInput = RoleFilter ?? "";
        ActiveFilterInput = ActiveFilter switch { true => "true", false => "false", _ => "" };
        DiscordLinkedFilterInput = DiscordLinkedFilter switch { true => "true", false => "false", _ => "" };
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        _resolvedQuery = CurrentQuery;

        var user = await GetUserAsync();
        var currentUserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(currentUserId))
        {
            Logger.LogWarning("User ID not found in claims");
            IsLoading = false;
            return;
        }

        _currentUserId = currentUserId;
        Logger.LogInformation("User {UserId} accessing user management list", currentUserId);

        var query = new UserSearchQueryDto
        {
            SearchTerm = SearchTerm,
            Role = RoleFilter,
            IsActive = ActiveFilter,
            IsDiscordLinked = DiscordLinkedFilter,
            Page = Math.Max(1, PageNumber),
            PageSize = PageSize,
            SortBy = "CreatedAt",
            SortDescending = true
        };

        var paginatedUsers = await UserManagementService.GetUsersAsync(query);
        AvailableRoles = await UserManagementService.GetAvailableRolesAsync(currentUserId);

        Users = paginatedUsers.Items;
        TotalCount = paginatedUsers.TotalCount;
        CurrentPage = paginatedUsers.Page;
        TotalPages = paginatedUsers.TotalPages;
        CanCreateUsers = user.IsInRole("Admin") || user.IsInRole("SuperAdmin");

        IsLoading = false;
    }

    private async Task<ClaimsPrincipal> GetUserAsync()
        => AuthenticationStateTask is not null
            ? (await AuthenticationStateTask).User
            : new ClaimsPrincipal(new ClaimsIdentity());

    protected void HandleFilterSubmit()
        => NavigationManager.NavigateTo(BuildUrl(SearchTermInput, RoleFilterInput, ParseTri(ActiveFilterInput), ParseTri(DiscordLinkedFilterInput), 1));

    protected void HandleClearFilters() => NavigationManager.NavigateTo("/Admin/Users");

    protected void HandlePageChanged(int page)
        => NavigationManager.NavigateTo(BuildUrl(SearchTerm, RoleFilter, ActiveFilter, DiscordLinkedFilter, page));

    private static bool? ParseTri(string value) => value switch { "true" => true, "false" => false, _ => null };

    private static string BuildUrl(string? searchTerm, string? roleFilter, bool? activeFilter, bool? discordLinkedFilter, int pageNumber)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(searchTerm))
        {
            parts.Add($"SearchTerm={Uri.EscapeDataString(searchTerm)}");
        }

        if (!string.IsNullOrEmpty(roleFilter))
        {
            parts.Add($"RoleFilter={Uri.EscapeDataString(roleFilter)}");
        }

        if (activeFilter.HasValue)
        {
            parts.Add($"ActiveFilter={activeFilter.Value}");
        }

        if (discordLinkedFilter.HasValue)
        {
            parts.Add($"DiscordLinkedFilter={discordLinkedFilter.Value}");
        }

        if (pageNumber > 1)
        {
            parts.Add($"pageNumber={pageNumber}");
        }

        return parts.Count == 0 ? "/Admin/Users" : $"/Admin/Users?{string.Join('&', parts)}";
    }

    /// <summary>
    /// Requests confirmation to flip a user's active status. The legacy <c>IndexModel.OnPostToggleActiveAsync</c>
    /// handler had no corresponding control in <c>Index.cshtml</c> (a dead POST handler, the same
    /// shape as the "dead OnPostSyncAsync" CLAUDE.md's gotchas warn about from the earlier Blazor
    /// attempt); this port gives it a real affordance instead of reproducing the gap.
    /// </summary>
    protected async Task RequestToggleActive(UserDto user)
    {
        _pendingToggleUser = user;
        var confirmed = _toggleModal is not null && await _toggleModal.ShowAsync();
        if (confirmed)
        {
            await HandleToggleConfirmed();
        }
    }

    private async Task HandleToggleConfirmed()
    {
        if (_pendingToggleUser is null || string.IsNullOrEmpty(_currentUserId))
        {
            return;
        }

        var targetUser = _pendingToggleUser;
        var targetActive = !targetUser.IsActive;
        var result = await UserManagementService.SetUserActiveStatusAsync(
            targetUser.Id,
            targetActive,
            _currentUserId,
            CircuitInfo.RemoteIp?.ToString());

        if (result.Succeeded)
        {
            Toast.Success($"User {(targetActive ? "enabled" : "disabled")} successfully");
            Logger.LogInformation("User {UserId} {Action} user {TargetUserId}", _currentUserId, targetActive ? "enabled" : "disabled", targetUser.Id);
            await LoadAsync();
        }
        else
        {
            Toast.Error(result.ErrorMessage ?? "Failed to update user status");
            Logger.LogWarning("Failed to toggle active status for user {UserId}: {Error}", targetUser.Id, result.ErrorMessage);
        }

        _pendingToggleUser = null;
    }

    private Task PersistCurrentResultAsync()
    {
        ApplicationState.PersistAsJson(PersistenceKey, new IndexPersistedState(Users, TotalCount, CurrentPage, TotalPages, AvailableRoles, CanCreateUsers, _currentUserId));
        return Task.CompletedTask;
    }

    protected List<SelectOption> RoleFilterOptions
    {
        get
        {
            var options = new List<SelectOption> { new() { Value = "", Text = "All Roles" } };
            options.AddRange(AvailableRoles.Select(r => new SelectOption { Value = r, Text = r }));
            return options;
        }
    }

    protected static readonly List<SelectOption> ActiveFilterOptions =
    [
        new() { Value = "", Text = "All" },
        new() { Value = "true", Text = "Active" },
        new() { Value = "false", Text = "Inactive" }
    ];

    protected static readonly List<SelectOption> DiscordLinkedFilterOptions =
    [
        new() { Value = "", Text = "All" },
        new() { Value = "true", Text = "Linked" },
        new() { Value = "false", Text = "Not Linked" }
    ];

    protected static BadgeVariant RoleBadgeVariant(string role) => role switch
    {
        "SuperAdmin" => BadgeVariant.Error,
        "Admin" => BadgeVariant.Orange,
        "Moderator" => BadgeVariant.Blue,
        "Viewer" => BadgeVariant.Info,
        _ => BadgeVariant.Default
    };

    protected static string UserDetailsUrl(string id) => $"/Admin/Users/Details?id={Uri.EscapeDataString(id)}";

    protected static string UserEditUrl(string id) => $"/Admin/Users/Edit?id={Uri.EscapeDataString(id)}";

    public void Dispose()
    {
        _persistingSubscription.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record IndexPersistedState(
        IReadOnlyList<UserDto> Users,
        int TotalCount,
        int CurrentPage,
        int TotalPages,
        IReadOnlyList<string> AvailableRoles,
        bool CanCreateUsers,
        string? CurrentUserId);
}
