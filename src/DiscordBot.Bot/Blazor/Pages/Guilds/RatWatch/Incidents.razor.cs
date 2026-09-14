using System.Text;
using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.RatWatch;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/RatWatch/Incidents.cshtml</c> +
/// <c>IncidentsModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Read-only
/// (unlike <c>RatWatch/Index.razor.cs</c>, this page has no cancel/end-vote actions, matching the
/// legacy page model, which exposed no mutation handler either) - filters, an incident detail
/// modal loaded on demand from <see cref="IRatWatchService.GetByIdAsync"/>, and a server-built CSV
/// export of the currently loaded page.
/// </summary>
public partial class Incidents : GuildPageBase
{
    private const int DefaultPageSize = 25;

    [SupplyParameterFromQuery(Name = "Statuses")]
    [Parameter]
    public int[]? StatusValues { get; set; }

    [SupplyParameterFromQuery(Name = "StartDate")]
    [Parameter]
    public DateTime? StartDate { get; set; }

    [SupplyParameterFromQuery(Name = "EndDate")]
    [Parameter]
    public DateTime? EndDate { get; set; }

    [SupplyParameterFromQuery(Name = "AccusedUser")]
    [Parameter]
    public string? AccusedUser { get; set; }

    [SupplyParameterFromQuery(Name = "InitiatorUser")]
    [Parameter]
    public string? InitiatorUser { get; set; }

    [SupplyParameterFromQuery(Name = "MinVoteCount")]
    [Parameter]
    public int? MinVoteCount { get; set; }

    [SupplyParameterFromQuery(Name = "Keyword")]
    [Parameter]
    public string? Keyword { get; set; }

    [SupplyParameterFromQuery(Name = "sortBy")]
    [Parameter]
    public string? SortBy { get; set; }

    [SupplyParameterFromQuery(Name = "sortDescending")]
    [Parameter]
    public bool? SortDescending { get; set; }

    [SupplyParameterFromQuery(Name = "pageNumber")]
    [Parameter]
    public int? PageNumber { get; set; }

    /// <summary>Legacy <c>?page=</c>/<c>?pageSize=</c> fallback query names.</summary>
    [SupplyParameterFromQuery(Name = "page")]
    [Parameter]
    public int? LegacyPage { get; set; }

    [SupplyParameterFromQuery(Name = "pageSize")]
    [Parameter]
    public int? PageSizeValue { get; set; }

    [Inject]
    private IRatWatchService Service { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private BrowserInterop BrowserInterop { get; set; } = default!;

    [Inject]
    private ILogger<Incidents> Logger { get; set; } = default!;

    protected IReadOnlyList<RatWatchItemViewModel> Items { get; private set; } = [];
    protected int TotalCount { get; private set; }
    protected bool LoadFailed { get; private set; }
    protected PagedQuery Query { get; private set; } = new(1, DefaultPageSize, "ScheduledAt", true);
    protected int TotalPages => Query.PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / Query.PageSize) : 0;
    protected int VotingDurationMinutes { get; private set; } = 5;

    protected RatWatchDto? ModalDetail { get; private set; }
    protected bool ModalOpen { get; private set; }
    protected bool ModalLoading { get; private set; }

    protected IReadOnlyList<RatWatchStatus> SelectedStatuses => StatusValues?.Select(v => (RatWatchStatus)v).ToList() ?? [];

    /// <summary>
    /// True when neither <see cref="StartDate"/> nor <see cref="EndDate"/> was supplied - only
    /// then does the legacy page's own "last 30 days" default apply to both ends together; a
    /// single date supplied alone leaves the other end unbounded, exactly like <c>IncidentsModel</c>.
    /// </summary>
    private bool UsingDefaultDateRange => !StartDate.HasValue && !EndDate.HasValue;

    protected DateTime? EffectiveStartDate => UsingDefaultDateRange ? DateTime.Today.AddDays(-30) : StartDate;

    protected DateTime? EffectiveEndDate => UsingDefaultDateRange ? DateTime.Today : EndDate;

    private object _resolvedQuery = new();
    private int _loadGeneration;

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        VotingDurationMinutes = (await Service.GetGuildSettingsAsync((ulong)GuildId)).VotingDurationMinutes;
        await LoadAsync();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (Guild is not null && !Equals(_resolvedQuery, CurrentQueryKey()))
        {
            _ = InvokeAsync(ReloadAndRerenderAsync);
        }
    }

    private async Task ReloadAndRerenderAsync()
    {
        await LoadAsync();
        StateHasChanged();
    }

    private (string, DateTime?, DateTime?, string?, string?, int?, string?, string?, bool?, int?, int?) CurrentQueryKey()
        => (string.Join(',', StatusValues ?? []), StartDate, EndDate, AccusedUser, InitiatorUser, MinVoteCount, Keyword, SortBy, SortDescending, PageNumber, LegacyPage);

    private async Task LoadAsync()
    {
        var generation = ++_loadGeneration;
        LoadFailed = false;
        _resolvedQuery = CurrentQueryKey();
        Query = PagedQuery.FromQuery(PageNumber, PageSizeValue, SortBy ?? "ScheduledAt", SortDescending ?? true, DefaultPageSize, LegacyPage);
        SeedFilterInputs();

        // End-of-day normalisation for inclusive date filtering, matching the legacy page model.
        var normalizedEndDate = EffectiveEndDate.HasValue ? EffectiveEndDate.Value.Date.AddDays(1).AddTicks(-1) : (DateTime?)null;

        var filter = new RatWatchIncidentFilterDto
        {
            Statuses = SelectedStatuses.Count > 0 ? SelectedStatuses.ToList() : null,
            StartDate = EffectiveStartDate,
            EndDate = normalizedEndDate,
            AccusedUser = AccusedUser,
            InitiatorUser = InitiatorUser,
            MinVoteCount = MinVoteCount,
            Keyword = Keyword,
            Page = Query.PageNumber,
            PageSize = Query.PageSize,
            SortBy = Query.SortBy ?? "ScheduledAt",
            SortDescending = Query.SortDescending
        };

        try
        {
            var (incidents, totalCount) = await Service.GetFilteredByGuildAsync((ulong)GuildId, filter);
            if (generation != _loadGeneration)
            {
                return;
            }

            Items = incidents.Select(dto => RatWatchItemViewModel.FromDto(dto, VotingDurationMinutes)).ToList();
            TotalCount = totalCount;
            RequestLocalTimeScan();
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            Logger.LogError(ex, "Failed to load Rat Watch incidents for guild {GuildId}", GuildId);
            LoadFailed = true;
            Toast.Error("Failed to load Rat Watch incidents.");
        }
    }

    protected string PageUrl => $"/Guilds/RatWatch/Incidents/{GuildId}{FilterSuffix}";

    private string FilterSuffix
    {
        get
        {
            var parts = new List<string>();
            foreach (var status in StatusValues ?? [])
            {
                parts.Add($"Statuses={status}");
            }

            if (StartDate.HasValue) parts.Add($"StartDate={StartDate:yyyy-MM-dd}");
            if (EndDate.HasValue) parts.Add($"EndDate={EndDate:yyyy-MM-dd}");
            if (!string.IsNullOrEmpty(AccusedUser)) parts.Add($"AccusedUser={Uri.EscapeDataString(AccusedUser)}");
            if (!string.IsNullOrEmpty(InitiatorUser)) parts.Add($"InitiatorUser={Uri.EscapeDataString(InitiatorUser)}");
            if (MinVoteCount.HasValue) parts.Add($"MinVoteCount={MinVoteCount}");
            if (!string.IsNullOrEmpty(Keyword)) parts.Add($"Keyword={Uri.EscapeDataString(Keyword)}");
            return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
        }
    }

    protected async Task OpenModalAsync(Guid incidentId)
    {
        ModalOpen = true;
        ModalLoading = true;
        ModalDetail = null;
        StateHasChanged();

        try
        {
            ModalDetail = await Service.GetByIdAsync(incidentId);
            RequestLocalTimeScan();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load Rat Watch incident {IncidentId}", incidentId);
            Toast.Error("Failed to load incident details.");
            ModalOpen = false;
        }
        finally
        {
            ModalLoading = false;
        }
    }

    protected void CloseModal()
    {
        ModalOpen = false;
        ModalDetail = null;
    }

    /// <summary>
    /// Builds the export CSV server-side from the currently loaded page's rows (the legacy page
    /// exported the same client-side, from an embedded JSON script block) - same columns
    /// (Date/Accused/Initiator/Status/Votes For/Votes Against/Custom Message), BOM included for
    /// Excel compatibility, handed to <see cref="BrowserInterop.DownloadFileAsync"/>.
    /// </summary>
    protected async Task ExportCsvAsync()
    {
        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendLine(string.Join(',', "Date", "Accused", "Initiator", "Status", "Votes For", "Votes Against", "Custom Message"));

        foreach (var item in Items)
        {
            sb.AppendLine(string.Join(',',
                EscapeCsv(item.ScheduledAt.ToString("yyyy-MM-dd HH:mm")),
                EscapeCsv(item.AccusedUsername),
                EscapeCsv(item.InitiatorUsername),
                EscapeCsv(item.StatusText),
                item.GuiltyVotes,
                item.NotGuiltyVotes,
                EscapeCsv(item.CustomMessage ?? string.Empty)));
        }

        var fileName = $"ratwatch-incidents-{GuildId}-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        await BrowserInterop.DownloadFileAsync(fileName, sb.ToString(), "text/csv");
    }

    private static string EscapeCsv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    protected static readonly List<(RatWatchStatus Status, string Label)> AllStatuses =
        Enum.GetValues<RatWatchStatus>().Select(s => (s, StatusLabel(s))).ToList();

    private static string StatusLabel(RatWatchStatus status) => status switch
    {
        RatWatchStatus.Pending => "Pending",
        RatWatchStatus.ClearedEarly => "Cleared Early",
        RatWatchStatus.Voting => "Voting",
        RatWatchStatus.Guilty => "Guilty",
        RatWatchStatus.NotGuilty => "Not Guilty",
        RatWatchStatus.Expired => "Expired",
        RatWatchStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };
}
