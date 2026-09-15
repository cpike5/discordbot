using System.Security.Claims;
using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Pages.Admin.Notifications;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Admin/Notifications/Index.cshtml</c> +
/// <c>IndexModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Every mutation
/// (single or bulk) goes through <see cref="ScopedOperations"/> and reloads the current page in
/// place - no full navigation, matching <c>notification-history.js</c>'s in-place DOM updates.
/// </summary>
public partial class Index : ComponentBase
{
    // Bound as int?/string, not NotificationType?/AlertSeverity?/ulong? directly -
    // QueryParameterValueSupplier only knows a fixed set of primitive types for
    // [SupplyParameterFromQuery] (string/bool/DateTime/decimal/double/float/Guid/int/long and
    // their nullable/array forms) and throws InvalidOperationException for an arbitrary enum or a
    // ulong (a Discord snowflake) - see Guilds/AudioModerationLog/Index.razor.cs's identical note.
    [SupplyParameterFromQuery(Name = "Type")]
    [Parameter]
    public int? TypeQuery { get; set; }

    protected NotificationType? Type => TypeQuery.HasValue ? (NotificationType)TypeQuery.Value : null;

    [SupplyParameterFromQuery(Name = "IsRead")]
    [Parameter]
    public bool? IsRead { get; set; }

    [SupplyParameterFromQuery(Name = "Severity")]
    [Parameter]
    public int? SeverityQuery { get; set; }

    protected AlertSeverity? Severity => SeverityQuery.HasValue ? (AlertSeverity)SeverityQuery.Value : null;

    [SupplyParameterFromQuery(Name = "StartDate")]
    [Parameter]
    public DateTime? StartDate { get; set; }

    [SupplyParameterFromQuery(Name = "EndDate")]
    [Parameter]
    public DateTime? EndDate { get; set; }

    [SupplyParameterFromQuery(Name = "SearchTerm")]
    [Parameter]
    public string? SearchTerm { get; set; }

    [SupplyParameterFromQuery(Name = "GuildId")]
    [Parameter]
    public string? GuildIdQuery { get; set; }

    protected ulong? GuildId => ulong.TryParse(GuildIdQuery, out var guildId) ? guildId : null;

    [SupplyParameterFromQuery(Name = "pageNumber")]
    [Parameter]
    public int? PageNumber { get; set; }

    [SupplyParameterFromQuery(Name = "pageSize")]
    [Parameter]
    public int? PageSize { get; set; }

    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject] private INotificationService NotificationService { get; set; } = default!;
    [Inject] private IGuildService GuildService { get; set; } = default!;
    [Inject] private IServiceScopeFactory ScopeFactory { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IToastService Toast { get; set; } = default!;
    [Inject] private BrowserInterop BrowserInterop { get; set; } = default!;
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    private const int DefaultPageSize = 25;

    protected IReadOnlyList<NotificationListItem> Notifications { get; private set; } = [];
    protected int TotalCount { get; private set; }
    protected int TotalPages { get; private set; } = 1;
    protected bool IsLoading { get; private set; } = true;
    protected bool LoadFailed { get; private set; }
    protected IReadOnlyList<GuildDto> AvailableGuilds { get; private set; } = [];
    protected PagedQuery Query { get; private set; } = PagedQuery.FromQuery(1, DefaultPageSize, defaultPageSize: DefaultPageSize);
    protected HashSet<Guid> SelectedIds { get; } = [];

    protected string TypeInput { get; set; } = "";
    protected string IsReadInput { get; set; } = "";
    protected string SeverityInput { get; set; } = "";
    protected string GuildIdInput { get; set; } = "";
    protected string? SearchTermInput { get; set; }
    protected DateOnly? StartInput { get; set; }
    protected DateOnly? EndInput { get; set; }

    private ConfirmModal? _deleteAllModal;
    private string? _userId;
    private bool _needsLocalTimeScan;
    private (NotificationType?, bool?, AlertSeverity?, DateTime?, DateTime?, string?, ulong?, int?, int?) _resolvedQuery;

    protected List<SelectOption> TypeOptions =>
    [
        new() { Value = "", Text = "All Types" },
        .. Enum.GetValues<NotificationType>().Select(t => new SelectOption { Value = t.ToString(), Text = t.ToString() })
    ];

    protected static readonly List<SelectOption> ReadOptions =
    [
        new() { Value = "", Text = "All" },
        new() { Value = "true", Text = "Read" },
        new() { Value = "false", Text = "Unread" }
    ];

    protected List<SelectOption> SeverityOptions =>
    [
        new() { Value = "", Text = "All Severities" },
        .. Enum.GetValues<AlertSeverity>().Select(s => new SelectOption { Value = s.ToString(), Text = s.ToString() })
    ];

    protected List<SelectOption> GuildOptions =>
    [
        new() { Value = "", Text = "All Guilds" },
        .. AvailableGuilds.Select(g => new SelectOption { Value = g.Id.ToString(), Text = g.Name })
    ];

    protected string PageUrl => BuildUrl(1);

    protected override async Task OnInitializedAsync()
    {
        SeedInputsFromQuery();
        AvailableGuilds = await GuildService.GetAllGuildsAsync();
        await LoadAsync(NotificationService);
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_resolvedQuery != CurrentQuery)
        {
            SeedInputsFromQuery();
            await LoadAsync(NotificationService);
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

    private (NotificationType?, bool?, AlertSeverity?, DateTime?, DateTime?, string?, ulong?, int?, int?) CurrentQuery =>
        (Type, IsRead, Severity, StartDate, EndDate, SearchTerm, GuildId, PageNumber, PageSize);

    private void SeedInputsFromQuery()
    {
        TypeInput = Type?.ToString() ?? "";
        IsReadInput = IsRead switch { true => "true", false => "false", _ => "" };
        SeverityInput = Severity?.ToString() ?? "";
        GuildIdInput = GuildId?.ToString() ?? "";
        SearchTermInput = SearchTerm;
        StartInput = StartDate.HasValue ? DateOnly.FromDateTime(StartDate.Value) : null;
        EndInput = EndDate.HasValue ? DateOnly.FromDateTime(EndDate.Value) : null;
    }

    private async Task<ClaimsPrincipal> GetUserAsync()
        => AuthenticationStateTask is not null ? (await AuthenticationStateTask).User : new ClaimsPrincipal(new ClaimsIdentity());

    private async Task LoadAsync(INotificationService service)
    {
        IsLoading = true;
        LoadFailed = false;
        _resolvedQuery = CurrentQuery;
        StateHasChanged();

        try
        {
            var user = await GetUserAsync();
            _userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(_userId))
            {
                LoadFailed = true;
                return;
            }

            var startDate = StartDate;
            var endDate = EndDate;
            var hasFilters = Type.HasValue || IsRead.HasValue || Severity.HasValue || StartDate.HasValue ||
                EndDate.HasValue || !string.IsNullOrWhiteSpace(SearchTerm) || GuildId.HasValue;
            if (!hasFilters)
            {
                startDate = DateTime.UtcNow.AddDays(-7);
                endDate = DateTime.UtcNow;
            }

            Query = PagedQuery.FromQuery(PageNumber, PageSize, defaultPageSize: DefaultPageSize);

            var query = new NotificationQueryDto
            {
                Type = Type,
                IsRead = IsRead,
                Severity = Severity,
                StartDate = startDate,
                EndDate = endDate?.Date.AddDays(1).AddTicks(-1),
                SearchTerm = SearchTerm,
                GuildId = GuildId,
                Page = Query.PageNumber,
                PageSize = Query.PageSize
            };

            var result = await service.GetUserNotificationsPagedAsync(_userId, query);
            var vm = NotificationListViewModel.FromPaginatedDto(result);

            Notifications = vm.Notifications;
            TotalCount = vm.TotalCount;
            TotalPages = Math.Max(1, vm.TotalPages);
            SelectedIds.Clear();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading notifications");
            LoadFailed = true;
        }
        finally
        {
            IsLoading = false;
            _needsLocalTimeScan = true;
        }
    }

    protected void ToggleSelected(Guid id, bool selected)
    {
        if (selected)
        {
            SelectedIds.Add(id);
        }
        else
        {
            SelectedIds.Remove(id);
        }
    }

    protected async Task ToggleReadAsync(NotificationListItem n)
    {
        if (_userId is null)
        {
            return;
        }

        if (n.IsRead)
        {
            await ScopeFactory.RunAsync<INotificationService>(s => s.MarkAsUnreadAsync(_userId, n.Id));
        }
        else
        {
            await ScopeFactory.RunAsync<INotificationService>(s => s.MarkAsReadAsync(_userId, n.Id));
        }

        await ScopeFactory.RunAsync<INotificationService>(LoadAsync);
    }

    protected async Task DeleteOneAsync(Guid id)
    {
        if (_userId is null)
        {
            return;
        }

        await ScopeFactory.RunAsync<INotificationService>(s => s.DeleteAsync(_userId, id));
        Toast.Success("Notification deleted");
        await ScopeFactory.RunAsync<INotificationService>(LoadAsync);
    }

    protected async Task MarkSelectedReadAsync()
    {
        if (_userId is null || SelectedIds.Count == 0)
        {
            return;
        }

        await ScopeFactory.RunAsync<INotificationService>(s => s.MarkMultipleAsReadAsync(_userId, SelectedIds.ToList()));
        Toast.Success("Selected notifications marked read");
        await ScopeFactory.RunAsync<INotificationService>(LoadAsync);
    }

    protected async Task DeleteSelectedAsync()
    {
        if (_userId is null || SelectedIds.Count == 0)
        {
            return;
        }

        var count = SelectedIds.Count;
        await ScopeFactory.RunAsync<INotificationService>(s => s.DeleteMultipleAsync(_userId, SelectedIds.ToList()));
        Toast.Success($"{count} notification(s) deleted");
        await ScopeFactory.RunAsync<INotificationService>(LoadAsync);
    }

    protected async Task MarkAllReadAsync()
    {
        if (_userId is null)
        {
            return;
        }

        await ScopeFactory.RunAsync<INotificationService>(s => s.MarkAllAsReadAsync(_userId));
        Toast.Success("All notifications marked read");
        await ScopeFactory.RunAsync<INotificationService>(LoadAsync);
    }

    protected async Task RequestDeleteAll()
    {
        var confirmed = _deleteAllModal is not null && await _deleteAllModal.ShowAsync();
        if (confirmed)
        {
            await HandleDeleteAllConfirmed();
        }
    }

    private async Task HandleDeleteAllConfirmed()
    {
        if (_userId is null)
        {
            return;
        }

        var deleted = await ScopeFactory.RunAsync<INotificationService, int>(s => s.DeleteAllAsync(_userId));
        Toast.Success($"Deleted {deleted} notification(s)");
        await ScopeFactory.RunAsync<INotificationService>(LoadAsync);
    }

    protected void HandleFilterSubmit() => NavigationManager.NavigateTo(BuildUrl(1));

    protected void HandleClearFilters() => NavigationManager.NavigateTo("/Admin/Notifications");

    private string BuildUrl(int page)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(TypeInput) && Enum.TryParse<NotificationType>(TypeInput, out var typeValue))
        {
            parts.Add($"Type={(int)typeValue}");
        }

        if (!string.IsNullOrEmpty(IsReadInput))
        {
            parts.Add($"IsRead={IsReadInput}");
        }

        if (!string.IsNullOrEmpty(SeverityInput) && Enum.TryParse<AlertSeverity>(SeverityInput, out var severityValue))
        {
            parts.Add($"Severity={(int)severityValue}");
        }

        if (!string.IsNullOrEmpty(GuildIdInput))
        {
            parts.Add($"GuildId={GuildIdInput}");
        }

        if (!string.IsNullOrEmpty(SearchTermInput))
        {
            parts.Add($"SearchTerm={Uri.EscapeDataString(SearchTermInput)}");
        }

        if (StartInput.HasValue)
        {
            parts.Add($"StartDate={StartInput.Value:yyyy-MM-dd}");
        }

        if (EndInput.HasValue)
        {
            parts.Add($"EndDate={EndInput.Value:yyyy-MM-dd}");
        }

        if (page > 1)
        {
            parts.Add($"pageNumber={page}");
        }

        return parts.Count == 0 ? "/Admin/Notifications" : $"/Admin/Notifications?{string.Join('&', parts)}";
    }
}
