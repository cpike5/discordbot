using System.Text;
using Discord.WebSocket;
using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.Members;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/Members/Index.cshtml</c> +
/// <c>IndexModel</c> + <c>_MemberDetailModal.cshtml</c> (docs/plans/blazor-port-plan.md §5 Phase 4,
/// cluster 4d). Filters are edited locally and only take effect on "Apply Filters" (building a new
/// query string and navigating), matching the legacy page's explicit submit button rather than the
/// single-Select-immediate-navigate pattern <c>FeatureRequests/Index.razor.cs</c> uses for its one
/// filter - five independent filters navigating on every keystroke/click would be poor UX.
/// </summary>
public partial class Index : GuildPageBase
{
    private const int PageSize = 25;

    public static readonly Dictionary<string, string> SortOptions = new()
    {
        { "JoinedAt", "Join Date" },
        { "Username", "Username" },
        { "LastActiveAt", "Last Active" }
    };

    public static readonly Dictionary<string, string> ActivityOptions = new()
    {
        { "", "All Members" },
        { "active-today", "Active Today" },
        { "active-week", "Active This Week" },
        { "active-month", "Active This Month" },
        { "inactive-week", "Inactive 7+ Days" },
        { "inactive-month", "Inactive 30+ Days" },
        { "never-messaged", "Never Messaged" }
    };

    [SupplyParameterFromQuery(Name = "SearchTerm")]
    [Parameter]
    public string? SearchTerm { get; set; }

    [SupplyParameterFromQuery(Name = "RoleFilter")]
    [Parameter]
    public string[]? RoleFilter { get; set; }

    [SupplyParameterFromQuery(Name = "JoinedAfter")]
    [Parameter]
    public DateTime? JoinedAfter { get; set; }

    [SupplyParameterFromQuery(Name = "JoinedBefore")]
    [Parameter]
    public DateTime? JoinedBefore { get; set; }

    [SupplyParameterFromQuery(Name = "ActivityFilter")]
    [Parameter]
    public string? ActivityFilter { get; set; }

    [SupplyParameterFromQuery(Name = "SortBy")]
    [Parameter]
    public string? SortBy { get; set; }

    [SupplyParameterFromQuery(Name = "SortDescending")]
    [Parameter]
    public bool? SortDescending { get; set; }

    [SupplyParameterFromQuery(Name = "pageNumber")]
    [Parameter]
    public int? PageNumber { get; set; }

    /// <summary>Legacy <c>?page=</c> fallback, same convention as every other paged guild list.</summary>
    [SupplyParameterFromQuery(Name = "page")]
    [Parameter]
    public int? LegacyPage { get; set; }

    [Inject]
    private IGuildMemberService MemberService { get; set; } = default!;

    [Inject]
    private DiscordSocketClient DiscordClient { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = default!;

    [Inject]
    private BrowserInterop BrowserInterop { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private ILogger<Index> Logger { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    protected IReadOnlyList<GuildMemberDto> Members { get; private set; } = [];
    protected int TotalCount { get; private set; }
    protected int TotalMemberCount { get; private set; }
    protected List<GuildRoleDto> AvailableRoles { get; private set; } = [];
    protected bool LoadFailed { get; private set; }
    protected PagedQuery Query { get; private set; } = new(1, PageSize);
    protected int TotalPages => Query.PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / Query.PageSize) : 0;

    // Working filter state - only applied (via navigation) on ApplyFiltersAsync/ResetFiltersAsync.
    protected string? SearchTermInput { get; set; }
    protected HashSet<ulong> RoleFilterInput { get; private set; } = [];
    protected DateOnly? JoinedAfterInput { get; set; }
    protected DateOnly? JoinedBeforeInput { get; set; }
    protected string ActivityFilterInput { get; set; } = "";
    protected string SortByInput { get; set; } = "JoinedAt";
    protected bool SortDescendingInput { get; set; } = true;

    protected HashSet<ulong> Selected { get; } = [];

    protected GuildMemberDto? ModalMember { get; private set; }
    protected bool ModalOpen { get; private set; }
    protected bool ModalLoading { get; private set; }
    protected bool ModalFailed { get; private set; }

    private (string?, string, DateTime?, DateTime?, string?, string?, bool?, int?, int?) _resolvedQuery;
    private int _loadGeneration;

    protected bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchTerm) ||
        (RoleFilter?.Length ?? 0) > 0 ||
        JoinedAfter.HasValue ||
        JoinedBefore.HasValue ||
        !string.IsNullOrWhiteSpace(ActivityFilter);

    protected int ActiveFilterCount
    {
        get
        {
            var count = 0;
            if (!string.IsNullOrWhiteSpace(SearchTerm)) count++;
            if ((RoleFilter?.Length ?? 0) > 0) count++;
            if (JoinedAfter.HasValue) count++;
            if (JoinedBefore.HasValue) count++;
            if (!string.IsNullOrWhiteSpace(ActivityFilter)) count++;
            return count;
        }
    }

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        LoadRolesFromDiscordCache();
        SeedFilterInputsFromQuery();
        await LoadAsync(MemberService);
    }

    /// <summary>Same pattern as every other paged guild list's <c>OnParametersSet</c> override -
    /// see <c>FeatureRequests/Index.razor.cs</c>'s identical note.</summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (Guild is not null && _resolvedQuery != CurrentQueryKey)
        {
            SeedFilterInputsFromQuery();
            _ = InvokeAsync(ReloadAndRerenderAsync);
        }
    }

    private async Task ReloadAndRerenderAsync()
    {
        await LoadAsync(MemberService);
        StateHasChanged();
    }

    private (string?, string, DateTime?, DateTime?, string?, string?, bool?, int?, int?) CurrentQueryKey =>
        (SearchTerm, string.Join(',', RoleFilter ?? []), JoinedAfter, JoinedBefore, ActivityFilter, SortBy, SortDescending, PageNumber, LegacyPage);

    private void LoadRolesFromDiscordCache()
    {
        var discordGuild = DiscordClient.GetGuild((ulong)GuildId);
        AvailableRoles = discordGuild is null
            ? []
            : discordGuild.Roles
                .Where(r => !r.IsEveryone && !r.IsManaged)
                .OrderByDescending(r => r.Position)
                .Select(r => new GuildRoleDto { Id = r.Id, Name = r.Name, Color = r.Color.RawValue, Position = r.Position })
                .ToList();
    }

    private void SeedFilterInputsFromQuery()
    {
        SearchTermInput = SearchTerm;
        RoleFilterInput = (RoleFilter ?? []).Where(r => ulong.TryParse(r, out _)).Select(ulong.Parse).ToHashSet();
        JoinedAfterInput = JoinedAfter.HasValue ? DateOnly.FromDateTime(JoinedAfter.Value) : null;
        JoinedBeforeInput = JoinedBefore.HasValue ? DateOnly.FromDateTime(JoinedBefore.Value) : null;
        ActivityFilterInput = ActivityFilter ?? "";
        SortByInput = string.IsNullOrEmpty(SortBy) ? "JoinedAt" : SortBy;
        SortDescendingInput = SortDescending ?? true;
    }

    private async Task LoadAsync(IGuildMemberService service)
    {
        var generation = ++_loadGeneration;
        LoadFailed = false;
        _resolvedQuery = CurrentQueryKey;
        Selected.Clear();
        Query = PagedQuery.FromQuery(PageNumber, null, SortBy, SortDescending ?? true, PageSize, LegacyPage);

        var query = BuildQuery(Query.PageNumber, Query.PageSize);

        try
        {
            var result = await service.GetMembersAsync((ulong)GuildId, query);
            var totalMemberCount = await service.GetMemberCountAsync((ulong)GuildId, new GuildMemberQueryDto { IsActive = true });

            if (generation != _loadGeneration)
            {
                return;
            }

            Members = result.Items;
            TotalCount = result.TotalCount;
            TotalMemberCount = totalMemberCount;
            RequestLocalTimeScan();
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            Logger.LogError(ex, "Failed to load member directory for guild {GuildId}", GuildId);
            LoadFailed = true;
            Toast.Error("Failed to load members.");
        }
    }

    private GuildMemberQueryDto BuildQuery(int page, int pageSize)
    {
        var query = new GuildMemberQueryDto
        {
            SearchTerm = SearchTerm,
            RoleIds = (RoleFilter ?? []).Where(r => ulong.TryParse(r, out _)).Select(ulong.Parse).ToList(),
            JoinedAtStart = JoinedAfter,
            JoinedAtEnd = JoinedBefore?.AddDays(1).AddSeconds(-1),
            SortBy = string.IsNullOrEmpty(SortBy) ? "JoinedAt" : SortBy,
            SortDescending = SortDescending ?? true,
            Page = page,
            PageSize = pageSize,
            IsActive = true
        };

        if (query.RoleIds.Count == 0)
        {
            query.RoleIds = null;
        }

        ApplyActivityFilter(query);
        return query;
    }

    private void ApplyActivityFilter(GuildMemberQueryDto query)
    {
        if (string.IsNullOrWhiteSpace(ActivityFilter))
        {
            return;
        }

        var now = DateTime.UtcNow;
        switch (ActivityFilter)
        {
            case "active-today":
                query.LastActiveAtStart = now.Date;
                break;
            case "active-week":
                query.LastActiveAtStart = now.AddDays(-7);
                break;
            case "active-month":
                query.LastActiveAtStart = now.AddDays(-30);
                break;
            case "inactive-week":
                query.LastActiveAtEnd = now.AddDays(-7);
                break;
            case "inactive-month":
                query.LastActiveAtEnd = now.AddDays(-30);
                break;
            case "never-messaged":
                query.LastActiveAtEnd = null;
                break;
        }
    }

    protected void SetSortByInput(string? value) => SortByInput = string.IsNullOrEmpty(value) ? "JoinedAt" : value;

    protected void SetSortDescendingInput(string? value) => SortDescendingInput = value == "true";

    protected void ToggleRoleFilter(ulong roleId, bool isChecked)
    {
        if (isChecked)
        {
            RoleFilterInput.Add(roleId);
        }
        else
        {
            RoleFilterInput.Remove(roleId);
        }
    }

    protected void ApplyFilters()
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(SearchTermInput))
        {
            query.Add($"SearchTerm={Uri.EscapeDataString(SearchTermInput)}");
        }

        foreach (var roleId in RoleFilterInput)
        {
            query.Add($"RoleFilter={roleId}");
        }

        if (JoinedAfterInput.HasValue)
        {
            query.Add($"JoinedAfter={JoinedAfterInput.Value:yyyy-MM-dd}");
        }

        if (JoinedBeforeInput.HasValue)
        {
            query.Add($"JoinedBefore={JoinedBeforeInput.Value:yyyy-MM-dd}");
        }

        if (!string.IsNullOrEmpty(ActivityFilterInput))
        {
            query.Add($"ActivityFilter={ActivityFilterInput}");
        }

        if (SortByInput != "JoinedAt")
        {
            query.Add($"SortBy={SortByInput}");
        }

        if (!SortDescendingInput)
        {
            query.Add("SortDescending=false");
        }

        var suffix = query.Count == 0 ? string.Empty : "?" + string.Join('&', query);
        NavigationManager.NavigateTo($"/Guilds/{GuildId}/Members{suffix}");
    }

    protected void ResetFilters() => NavigationManager.NavigateTo($"/Guilds/{GuildId}/Members");

    protected string PageUrl => $"/Guilds/{GuildId}/Members{FilterSuffix}";

    private string FilterSuffix
    {
        get
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(SearchTerm)) query.Add($"SearchTerm={Uri.EscapeDataString(SearchTerm)}");
            foreach (var role in RoleFilter ?? []) query.Add($"RoleFilter={role}");
            if (JoinedAfter.HasValue) query.Add($"JoinedAfter={JoinedAfter.Value:yyyy-MM-dd}");
            if (JoinedBefore.HasValue) query.Add($"JoinedBefore={JoinedBefore.Value:yyyy-MM-dd}");
            if (!string.IsNullOrWhiteSpace(ActivityFilter)) query.Add($"ActivityFilter={ActivityFilter}");
            if (!string.IsNullOrEmpty(SortBy)) query.Add($"SortBy={SortBy}");
            if (SortDescending == false) query.Add("SortDescending=false");
            return query.Count == 0 ? string.Empty : "?" + string.Join('&', query);
        }
    }

    protected void ToggleSelect(ulong userId, bool isChecked)
    {
        if (isChecked)
        {
            Selected.Add(userId);
        }
        else
        {
            Selected.Remove(userId);
        }
    }

    protected bool AllOnPageSelected => Members.Count > 0 && Members.All(m => Selected.Contains(m.UserId));

    protected void ToggleSelectAll(bool isChecked)
    {
        if (isChecked)
        {
            foreach (var m in Members)
            {
                Selected.Add(m.UserId);
            }
        }
        else
        {
            foreach (var m in Members)
            {
                Selected.Remove(m.UserId);
            }
        }
    }

    protected void DeselectAll() => Selected.Clear();

    protected async Task ExportSelectedAsync()
    {
        if (Selected.Count == 0)
        {
            return;
        }

        var query = new GuildMemberQueryDto { UserIds = Selected.ToList(), IsActive = null };
        await ExportAsync(query, $"members-selected-{GuildId}");
    }

    protected async Task ExportAllFilteredAsync()
    {
        var query = BuildQuery(1, PageSize);
        query.Page = 1;
        await ExportAsync(query, $"members-{GuildId}");
    }

    private async Task ExportAsync(GuildMemberQueryDto query, string fileNamePrefix)
    {
        try
        {
            var csv = await ScopeFactory.RunAsync<IGuildMemberService, byte[]>(s => s.ExportMembersToCsvAsync((ulong)GuildId, query));
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            await BrowserInterop.DownloadFileAsync($"{fileNamePrefix}-{timestamp}.csv", Encoding.UTF8.GetString(csv), "text/csv");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export members for guild {GuildId}", GuildId);
            Toast.Error("Failed to export members.");
        }
    }

    protected async Task OpenMemberModalAsync(ulong userId)
    {
        ModalOpen = true;
        ModalLoading = true;
        ModalFailed = false;
        ModalMember = null;
        StateHasChanged();

        try
        {
            ModalMember = await MemberService.GetMemberAsync((ulong)GuildId, userId);
            ModalFailed = ModalMember is null;
            RequestLocalTimeScan();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load member {UserId} in guild {GuildId}", userId, GuildId);
            ModalFailed = true;
        }
        finally
        {
            ModalLoading = false;
        }
    }

    protected void CloseModal()
    {
        ModalOpen = false;
        ModalMember = null;
    }

    protected async Task CopyUserIdAsync(ulong userId)
    {
        var ok = await BrowserInterop.CopyToClipboardAsync(userId.ToString());
        if (ok)
        {
            Toast.Success("User ID copied to clipboard.");
        }
        else
        {
            Toast.Error("Couldn't copy the user ID.");
        }
    }

    protected static string? BuildAvatarUrl(GuildMemberDto member)
    {
        if (string.IsNullOrEmpty(member.AvatarHash))
        {
            return null;
        }

        var extension = member.AvatarHash.StartsWith("a_", StringComparison.Ordinal) ? "gif" : "png";
        return $"https://cdn.discordapp.com/avatars/{member.UserId}/{member.AvatarHash}.{extension}?size=80";
    }

    protected static string Initials(string displayName) =>
        displayName.Length >= 2 ? displayName[..2].ToUpperInvariant() : displayName.ToUpperInvariant();
}
