using System.Security.Claims;
using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Pages.Guilds;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/Index.cshtml</c> + <c>IndexModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Top-level (not guild-scoped), so it
/// inherits <see cref="ComponentBase"/> directly under <c>MainLayout</c> rather than
/// <c>GuildPageBase</c>/<c>GuildLayout</c> - the same shape as <c>Blazor/Pages/Admin/Users/Index.razor.cs</c>.
/// </summary>
public partial class Index : ComponentBase, IDisposable
{
    private const int PageSize = 10;

    [SupplyParameterFromQuery(Name = "SearchTerm")]
    [Parameter]
    public string? SearchTerm { get; set; }

    [SupplyParameterFromQuery(Name = "StatusFilter")]
    [Parameter]
    public bool? StatusFilter { get; set; }

    [SupplyParameterFromQuery(Name = "SortBy")]
    [Parameter]
    public string? SortBy { get; set; }

    [SupplyParameterFromQuery(Name = "SortDescending")]
    [Parameter]
    public bool? SortDescending { get; set; }

    [SupplyParameterFromQuery(Name = "pageNumber")]
    [Parameter]
    public int? PageNumber { get; set; }

    /// <summary>Legacy <c>?page=</c> fallback, used only when <see cref="PageNumber"/> is absent.</summary>
    [SupplyParameterFromQuery(Name = "page")]
    [Parameter]
    public int? LegacyPage { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private IGuildService GuildService { get; set; } = default!;

    [Inject]
    private UserManager<ApplicationUser> UserManager { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private PersistentComponentState ApplicationState { get; set; } = default!;

    [Inject]
    private BrowserInterop BrowserInterop { get; set; } = default!;

    [Inject]
    private ILogger<Index> Logger { get; set; } = default!;

    private bool _needsLocalTimeScan;

    protected IReadOnlyList<GuildSummaryItem> Guilds { get; private set; } = [];
    protected int TotalCount { get; private set; }
    protected int TotalPages => Query.PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / Query.PageSize) : 0;
    protected PagedQuery Query { get; private set; } = new(1, PageSize);
    protected bool IsFiltered { get; private set; }
    protected int TotalGuilds { get; private set; }
    protected bool CanSyncAll { get; private set; }
    protected HashSet<ulong> SyncingGuildIds { get; } = [];
    protected bool IsSyncingAll { get; private set; }

    private (string?, bool?, string?, bool?, int?, int?) _resolvedQuery;
    private PersistingComponentStateSubscription _persistingSubscription;
    private const string PersistenceKey = "Guilds.Index";

    protected override async Task OnInitializedAsync()
    {
        _persistingSubscription = ApplicationState.RegisterOnPersisting(PersistCurrentResultAsync);

        if (ApplicationState.TryTakeFromJson<IndexPersistedState>(PersistenceKey, out var restored) && restored is not null)
        {
            Guilds = restored.Guilds;
            TotalCount = restored.TotalCount;
            Query = restored.Query;
            IsFiltered = restored.IsFiltered;
            TotalGuilds = restored.TotalGuilds;
            CanSyncAll = restored.CanSyncAll;
            _resolvedQuery = CurrentQueryKey;
            _needsLocalTimeScan = true;
        }
        else
        {
            await LoadAsync(GuildService);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_resolvedQuery != CurrentQueryKey)
        {
            await LoadAsync(GuildService);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_needsLocalTimeScan)
        {
            _needsLocalTimeScan = false;
            await BrowserInterop.ConvertLocalTimesAsync();
        }
    }

    private (string?, bool?, string?, bool?, int?, int?) CurrentQueryKey
        => (SearchTerm, StatusFilter, SortBy, SortDescending, PageNumber, LegacyPage);

    private async Task<ClaimsPrincipal> GetUserAsync()
        => AuthenticationStateTask is not null
            ? (await AuthenticationStateTask).User
            : new ClaimsPrincipal(new ClaimsIdentity());

    private async Task LoadAsync(IGuildService service)
    {
        _resolvedQuery = CurrentQueryKey;
        Query = PagedQuery.FromQuery(PageNumber, null, SortBy, SortDescending ?? false, PageSize, LegacyPage);

        var principal = await GetUserAsync();
        var user = await UserManager.GetUserAsync(principal);
        var userRoles = user is not null ? await UserManager.GetRolesAsync(user) : [];

        IsFiltered = userRoles.Count > 0
            && !userRoles.Contains("SuperAdmin")
            && !userRoles.Contains("Admin")
            && (userRoles.Contains("Moderator") || userRoles.Contains("Viewer"));

        if (IsFiltered)
        {
            var allGuilds = await service.GetAllGuildsAsync();
            TotalGuilds = allGuilds.Count;
        }

        CanSyncAll = userRoles.Contains("Admin") || userRoles.Contains("SuperAdmin");

        var query = new GuildSearchQueryDto
        {
            SearchTerm = SearchTerm,
            IsActive = StatusFilter,
            Page = Query.PageNumber,
            PageSize = Query.PageSize,
            SortBy = Query.SortBy ?? "Name",
            SortDescending = Query.SortDescending,
            UserId = user?.Id,
            UserRoles = userRoles
        };

        var result = await service.GetGuildsAsync(query);
        Guilds = result.Items.Select(GuildSummaryItem.FromDto).ToList();
        TotalCount = result.TotalCount;
        _needsLocalTimeScan = true;
    }

    protected async Task HandleSyncGuildAsync(ulong guildId)
    {
        if (!SyncingGuildIds.Add(guildId))
        {
            return;
        }

        StateHasChanged();

        try
        {
            var success = await ScopeFactory.RunAsync<IGuildService, bool>(s => s.SyncGuildAsync(guildId));
            if (success)
            {
                Toast.Success("Guild synced successfully.");
                await ScopeFactory.RunAsync<IGuildService>(LoadAsync);
            }
            else
            {
                Toast.Error("Guild not found in Discord client.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error syncing guild {GuildId}", guildId);
            Toast.Error("An error occurred while syncing the guild.");
        }
        finally
        {
            SyncingGuildIds.Remove(guildId);
        }
    }

    protected async Task HandleSyncAllAsync()
    {
        if (!CanSyncAll || IsSyncingAll)
        {
            return;
        }

        IsSyncingAll = true;

        try
        {
            var syncedCount = await ScopeFactory.RunAsync<IGuildService, int>(s => s.SyncAllGuildsAsync());
            Toast.Success($"Successfully synced {syncedCount} guild{(syncedCount != 1 ? "s" : "")}.");
            await ScopeFactory.RunAsync<IGuildService>(LoadAsync);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error syncing all guilds");
            Toast.Error("An error occurred while syncing guilds.");
        }
        finally
        {
            IsSyncingAll = false;
        }
    }

    protected static string DetailsUrl(ulong guildId) => $"/Guilds/Details/{guildId}";

    protected string PageUrl
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(SearchTerm))
            {
                parts.Add($"SearchTerm={Uri.EscapeDataString(SearchTerm)}");
            }

            if (StatusFilter.HasValue)
            {
                parts.Add($"StatusFilter={StatusFilter.Value}");
            }

            if (!string.IsNullOrEmpty(SortBy))
            {
                parts.Add($"SortBy={Uri.EscapeDataString(SortBy)}");
            }

            if (SortDescending == true)
            {
                parts.Add("SortDescending=true");
            }

            return parts.Count == 0 ? "/Guilds" : $"/Guilds?{string.Join('&', parts)}";
        }
    }

    private Task PersistCurrentResultAsync()
    {
        ApplicationState.PersistAsJson(PersistenceKey, new IndexPersistedState(Guilds, TotalCount, Query, IsFiltered, TotalGuilds, CanSyncAll));
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _persistingSubscription.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record IndexPersistedState(
        IReadOnlyList<GuildSummaryItem> Guilds,
        int TotalCount,
        PagedQuery Query,
        bool IsFiltered,
        int TotalGuilds,
        bool CanSyncAll);
}
