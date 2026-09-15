using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Utilities;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Pages.Admin.Logs.Tabs;

/// <summary>
/// Code-behind for <c>AuditTab.razor</c> - filters, paging, expand/collapse state and CSV export
/// link for the Logs page's "audit" tab. Reproduces <c>Pages/Admin/Logs/Index.cshtml.cs</c>'s
/// <c>LoadAuditLogsAsync</c>; CSV generation itself moved to
/// <c>Services/AdminLogsExportService.cs</c> behind the minimal-API export endpoint.
/// </summary>
public partial class AuditTab : ComponentBase
{
    private const int DefaultPageSize = 25;

    // Bound as int?, not AuditLogCategory?/AuditLogAction? directly - QueryParameterValueSupplier
    // throws for an arbitrary enum; see the identical note on MessagesTab.razor.cs's AuthorIdQuery.
    [SupplyParameterFromQuery(Name = "Category")]
    [Parameter]
    public int? CategoryQuery { get; set; }

    protected AuditLogCategory? Category => CategoryQuery.HasValue ? (AuditLogCategory)CategoryQuery.Value : null;

    [SupplyParameterFromQuery(Name = "Action")]
    [Parameter]
    public int? ActionQuery { get; set; }

    protected AuditLogAction? Action => ActionQuery.HasValue ? (AuditLogAction)ActionQuery.Value : null;

    [SupplyParameterFromQuery(Name = "ActorId")]
    [Parameter]
    public string? ActorId { get; set; }

    [SupplyParameterFromQuery(Name = "TargetType")]
    [Parameter]
    public string? TargetType { get; set; }

    // Bound as string, not ulong? - see the identical note on MessagesTab.razor.cs's AuthorIdQuery.
    [SupplyParameterFromQuery(Name = "AuditGuildId")]
    [Parameter]
    public string? AuditGuildIdQuery { get; set; }

    protected ulong? AuditGuildId => ulong.TryParse(AuditGuildIdQuery, out var guildId) ? guildId : null;

    [SupplyParameterFromQuery(Name = "AuditStartDate")]
    [Parameter]
    public DateTime? AuditStartDate { get; set; }

    [SupplyParameterFromQuery(Name = "AuditEndDate")]
    [Parameter]
    public DateTime? AuditEndDate { get; set; }

    [SupplyParameterFromQuery(Name = "AuditSearchTerm")]
    [Parameter]
    public string? AuditSearchTerm { get; set; }

    [SupplyParameterFromQuery(Name = "auditPageNumber")]
    [Parameter]
    public int AuditPageNumber { get; set; } = 1;

    [SupplyParameterFromQuery(Name = "AuditPageSize")]
    [Parameter]
    public int? AuditPageSizeParam { get; set; }

    [SupplyParameterFromQuery(Name = "UserTimezone")]
    [Parameter]
    public string? UserTimezone { get; set; }

    [Inject] private IAuditLogService AuditLogService { get; set; } = default!;
    [Inject] private IGuildService GuildService { get; set; } = default!;
    [Inject] private IMessageLogRepository MessageLogRepository { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private BrowserInterop BrowserInterop { get; set; } = default!;
    [Inject] private ILogger<AuditTab> Logger { get; set; } = default!;

    protected IReadOnlyList<AuditLogListItem> Logs { get; private set; } = [];
    protected int TotalCount { get; private set; }
    protected int PageNumber { get; private set; } = 1;
    protected int PageSize { get; private set; } = DefaultPageSize;
    protected int TotalPages => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
    protected bool IsLoading { get; private set; } = true;
    protected bool LoadFailed { get; private set; }
    protected IReadOnlyList<GuildDto> AvailableGuilds { get; private set; } = [];

    protected string CategoryInput { get; set; } = "";
    protected string ActionInput { get; set; } = "";
    protected string AuditGuildIdInput { get; set; } = "";
    protected string? ActorIdInput { get; set; }
    protected string? ActorDisplayText { get; set; }
    protected string? TargetTypeInput { get; set; }
    protected string? SearchTermInput { get; set; }
    protected DateOnly? StartInput { get; set; }
    protected DateOnly? EndInput { get; set; }

    private readonly HashSet<long> _expanded = [];
    private bool _needsLocalTimeScan;
    private bool _timezoneResolved;
    private (AuditLogCategory?, AuditLogAction?, string?, string?, ulong?, DateTime?, DateTime?, string?, int, int?) _resolvedQuery;

    protected List<SelectOption> CategoryOptions =>
    [
        new() { Value = "", Text = "All Categories" },
        .. Enum.GetValues<AuditLogCategory>().Select(c => new SelectOption { Value = c.ToString(), Text = c.ToString() })
    ];

    protected List<SelectOption> ActionOptions =>
    [
        new() { Value = "", Text = "All Actions" },
        .. Enum.GetValues<AuditLogAction>().Select(a => new SelectOption { Value = a.ToString(), Text = a.ToString() })
    ];

    protected List<SelectOption> GuildOptions =>
    [
        new() { Value = "", Text = "All Guilds" },
        .. AvailableGuilds.Select(g => new SelectOption { Value = g.Id.ToString(), Text = g.Name })
    ];

    protected string PageUrl => BuildUrl(1);

    protected string ExportUrl
    {
        get
        {
            var parts = new List<string>();
            AppendFilterParams(parts, ExportUrlEncode);
            return "/api/admin/audit-logs/export" + (parts.Count == 0 ? "" : "?" + string.Join('&', parts));
        }
    }

    protected override async Task OnInitializedAsync()
    {
        SeedInputsFromQuery();
        AvailableGuilds = await GuildService.GetAllGuildsAsync();
        await LoadAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (_resolvedQuery != CurrentQuery)
        {
            SeedInputsFromQuery();
            await LoadAsync();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_needsLocalTimeScan)
        {
            _needsLocalTimeScan = false;
            await BrowserInterop.ConvertLocalTimesAsync();
        }

        if (firstRender && !_timezoneResolved)
        {
            _timezoneResolved = true;
            try
            {
                var tz = await BrowserInterop.GetTimeZoneAsync();
                if (!string.IsNullOrEmpty(tz) && tz != UserTimezone)
                {
                    UserTimezone = tz;
                    await LoadAsync();
                    StateHasChanged();
                }
            }
            catch
            {
                // Timezone detection is best-effort; UTC-based filtering still works without it.
            }
        }
    }

    private (AuditLogCategory?, AuditLogAction?, string?, string?, ulong?, DateTime?, DateTime?, string?, int, int?) CurrentQuery =>
        (Category, Action, ActorId, TargetType, AuditGuildId, AuditStartDate, AuditEndDate, AuditSearchTerm, AuditPageNumber, AuditPageSizeParam);

    private void SeedInputsFromQuery()
    {
        CategoryInput = Category?.ToString() ?? "";
        ActionInput = Action?.ToString() ?? "";
        AuditGuildIdInput = AuditGuildId?.ToString() ?? "";
        ActorIdInput = ActorId;
        TargetTypeInput = TargetType;
        SearchTermInput = AuditSearchTerm;
        StartInput = AuditStartDate.HasValue ? DateOnly.FromDateTime(AuditStartDate.Value) : null;
        EndInput = AuditEndDate.HasValue ? DateOnly.FromDateTime(AuditEndDate.Value) : null;
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        LoadFailed = false;
        _resolvedQuery = CurrentQuery;
        StateHasChanged();

        try
        {
            var startDate = AuditStartDate;
            var endDate = AuditEndDate;
            if (!startDate.HasValue && !endDate.HasValue)
            {
                startDate = DateTime.UtcNow.Date.AddDays(-30);
                endDate = DateTime.UtcNow.Date.AddDays(1);
            }

            if (!string.IsNullOrEmpty(ActorId) && ulong.TryParse(ActorId, out var actorUserId))
            {
                var messages = await MessageLogRepository.GetUserMessagesAsync(actorUserId, limit: 1);
                ActorDisplayText = messages.FirstOrDefault()?.User?.Username;
            }

            DateTime? queryStartDate = startDate.HasValue
                ? TimezoneHelper.ConvertToUtc(startDate.Value.Date, UserTimezone)
                : null;
            DateTime? queryEndDate = endDate.HasValue
                ? TimezoneHelper.ConvertToUtc(endDate.Value.Date.AddDays(1).AddTicks(-1), UserTimezone)
                : null;

            PageSize = AuditPageSizeParam is > 0 and <= 100 ? AuditPageSizeParam.Value : DefaultPageSize;

            var query = new AuditLogQueryDto
            {
                Category = Category,
                Action = Action,
                ActorId = ActorId,
                TargetType = TargetType,
                GuildId = AuditGuildId,
                StartDate = queryStartDate,
                EndDate = queryEndDate,
                SearchTerm = AuditSearchTerm,
                Page = Math.Max(1, AuditPageNumber),
                PageSize = PageSize
            };

            var (items, totalCount) = await AuditLogService.GetLogsAsync(query);

            Logs = items.Select(AuditLogListItem.FromDto).ToList();
            TotalCount = totalCount;
            PageNumber = Math.Max(1, AuditPageNumber);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading audit logs");
            LoadFailed = true;
        }
        finally
        {
            IsLoading = false;
            _needsLocalTimeScan = true;
        }
    }

    protected async Task<IReadOnlyList<AutocompleteItem>> SearchActorsAsync(string term, CancellationToken ct)
    {
        var authors = await MessageLogRepository.SearchAuthorsAsync(term, guildId: null, limit: 25, ct);
        return authors.Select(a => new AutocompleteItem(a.UserId.ToString(), a.Username)).ToList();
    }

    protected void ToggleExpanded(long id)
    {
        if (!_expanded.Remove(id))
        {
            _expanded.Add(id);
        }
    }

    protected void HandleFilterSubmit() => NavigationManager.NavigateTo(BuildUrl(1));

    protected void HandleClearFilters() => NavigationManager.NavigateTo("/Admin/Logs?tab=audit");

    private void AppendFilterParams(List<string> parts, Func<string, string> encode)
    {
        if (!string.IsNullOrEmpty(CategoryInput) && Enum.TryParse<AuditLogCategory>(CategoryInput, out var categoryValue))
        {
            parts.Add($"Category={(int)categoryValue}");
        }

        if (!string.IsNullOrEmpty(ActionInput) && Enum.TryParse<AuditLogAction>(ActionInput, out var actionValue))
        {
            parts.Add($"Action={(int)actionValue}");
        }

        if (!string.IsNullOrEmpty(ActorIdInput))
        {
            parts.Add($"ActorId={encode(ActorIdInput)}");
        }

        if (!string.IsNullOrEmpty(TargetTypeInput))
        {
            parts.Add($"TargetType={encode(TargetTypeInput)}");
        }

        if (!string.IsNullOrEmpty(AuditGuildIdInput))
        {
            parts.Add($"AuditGuildId={encode(AuditGuildIdInput)}");
        }

        if (StartInput.HasValue)
        {
            parts.Add($"AuditStartDate={StartInput.Value:yyyy-MM-dd}");
        }

        if (EndInput.HasValue)
        {
            parts.Add($"AuditEndDate={EndInput.Value:yyyy-MM-dd}");
        }

        if (!string.IsNullOrEmpty(SearchTermInput))
        {
            parts.Add($"AuditSearchTerm={encode(SearchTermInput)}");
        }

        if (!string.IsNullOrEmpty(UserTimezone))
        {
            parts.Add($"UserTimezone={encode(UserTimezone)}");
        }
    }

    private static string ExportUrlEncode(string value) => Uri.EscapeDataString(value);

    private string BuildUrl(int page)
    {
        var parts = new List<string> { "tab=audit" };
        AppendFilterParams(parts, Uri.EscapeDataString);

        if (page > 1)
        {
            parts.Add($"auditPageNumber={page}");
        }

        return $"/Admin/Logs?{string.Join('&', parts)}";
    }
}
