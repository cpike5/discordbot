using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Pages.Admin.LlmUsage;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Admin/LlmUsage.cshtml</c> +
/// <c>LlmUsageModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Range
/// resolution/clamping is shared with the retired <c>LlmUsageController</c> via
/// <see cref="LlmUsageRangeHelper"/>. The per-user drill-down (<c>llm-usage.js</c>'s
/// <c>api/admin/llm-usage/records</c> call) is now <see cref="LoadDrilldownPageAsync"/>, a paged
/// component method straight over <see cref="ILlmUsageRepository.GetRecordsAsync"/>.
/// </summary>
public partial class Index : ComponentBase
{
    private const int DrilldownPageSizeConst = 20;

    [SupplyParameterFromQuery(Name = "StartDate")]
    [Parameter]
    public DateTime? StartDate { get; set; }

    [SupplyParameterFromQuery(Name = "EndDate")]
    [Parameter]
    public DateTime? EndDate { get; set; }

    // Bound as string/int?, not ulong?/LlmMode? directly - QueryParameterValueSupplier only knows
    // a fixed set of primitive types for [SupplyParameterFromQuery] and throws
    // InvalidOperationException for a ulong (a Discord snowflake) or an arbitrary enum - see the
    // identical note on Guilds/AudioModerationLog/Index.razor.cs.
    [SupplyParameterFromQuery(Name = "GuildId")]
    [Parameter]
    public string? GuildIdQuery { get; set; }

    protected ulong? GuildId => ulong.TryParse(GuildIdQuery, out var guildId) ? guildId : null;

    [SupplyParameterFromQuery(Name = "Mode")]
    [Parameter]
    public int? ModeQuery { get; set; }

    protected LlmMode? Mode => ModeQuery.HasValue ? (LlmMode)ModeQuery.Value : null;

    [Inject] private ILlmUsageRepository UsageRepository { get; set; } = default!;
    [Inject] private IDiscordUserResolver UserResolver { get; set; } = default!;
    [Inject] private IGuildService GuildService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private BrowserInterop BrowserInterop { get; set; } = default!;
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    protected bool IsLoading { get; private set; } = true;
    protected bool LoadFailed { get; private set; }
    protected IReadOnlyList<GuildDto> AvailableGuilds { get; private set; } = [];

    protected LlmUsageTotalsDto Totals { get; private set; } = new();
    protected IReadOnlyList<LlmUsageByUserDto> ByUser { get; private set; } = [];
    protected IReadOnlyList<LlmUsageByModelDto> ByModel { get; private set; } = [];
    protected IReadOnlyList<LlmUsageByModeDto> ByMode { get; private set; } = [];
    protected IReadOnlyList<LlmUsageByDayDto> ByDay { get; private set; } = [];

    protected DateTime StartInput { get; set; }
    protected DateTime EndInput { get; set; }
    protected string GuildIdInput { get; set; } = "";
    protected string ModeInput { get; set; } = "";

    protected string? SelectedUserId { get; set; }
    protected IReadOnlyList<LlmUsageRecord> DrilldownRecords { get; private set; } = [];
    protected int DrilldownPage { get; private set; } = 1;
    protected int DrilldownPageSize { get; private set; } = DrilldownPageSizeConst;
    protected int DrilldownTotalCount { get; private set; }
    protected int DrilldownTotalPages => Math.Max(1, (int)Math.Ceiling((double)DrilldownTotalCount / DrilldownPageSize));

    private bool _needsLocalTimeScan;
    private (DateTime?, DateTime?, ulong?, LlmMode?) _resolvedQuery;
    private (DateTime From, DateTime To) _resolvedRange;

    protected List<SelectOption> GuildOptions =>
    [
        new() { Value = "", Text = "All Guilds" },
        .. AvailableGuilds.Select(g => new SelectOption { Value = g.Id.ToString(), Text = g.Name })
    ];

    protected List<SelectOption> ModeOptions =>
    [
        new() { Value = "", Text = "All Modes" },
        .. Enum.GetValues<LlmMode>().Select(m => new SelectOption { Value = m.ToString(), Text = m.ToString() })
    ];

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
    }

    private (DateTime?, DateTime?, ulong?, LlmMode?) CurrentQuery => (StartDate, EndDate, GuildId, Mode);

    private void SeedInputsFromQuery()
    {
        GuildIdInput = GuildId?.ToString() ?? "";
        ModeInput = Mode?.ToString() ?? "";
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        LoadFailed = false;
        SelectedUserId = null;
        _resolvedQuery = CurrentQuery;
        StateHasChanged();

        try
        {
            _resolvedRange = LlmUsageRangeHelper.ResolveAndClamp(StartDate, EndDate);
            StartInput = _resolvedRange.From;
            EndInput = _resolvedRange.To;

            var query = new LlmUsageQuery
            {
                From = _resolvedRange.From,
                To = _resolvedRange.To.AddDays(1).AddTicks(-1),
                GuildId = GuildId,
                Mode = Mode
            };

            var totals = await UsageRepository.GetTotalsAsync(query);
            var byUser = await UsageRepository.GetByUserAsync(query, 25);
            var byModel = await UsageRepository.GetByModelAsync(query);
            var byMode = await UsageRepository.GetByModeAsync(query);
            var byDay = await UsageRepository.GetByDayAsync(query);
            var names = await UserResolver.ResolveUsersAsync(byUser.Select(u => u.UserId));

            Totals = new LlmUsageTotalsDto
            {
                MessageCount = totals.MessageCount,
                InputTokens = totals.InputTokens,
                OutputTokens = totals.OutputTokens,
                CachedTokens = totals.CachedTokens,
                CacheWriteTokens = totals.CacheWriteTokens,
                CostUsd = totals.CostUsd,
                BilledCostShare = totals.BilledCostShare,
                FailedCount = totals.FailedCount,
                AverageLatencyMs = totals.AverageLatencyMs
            };

            ByUser = byUser.Select(u =>
            {
                var (username, avatarUrl) = names.TryGetValue(u.UserId, out var resolved) ? resolved : ($"Unknown#{u.UserId}", null);
                return new LlmUsageByUserDto
                {
                    UserId = u.UserId.ToString(),
                    DisplayName = username,
                    AvatarUrl = avatarUrl,
                    MessageCount = u.MessageCount,
                    InputTokens = u.InputTokens,
                    OutputTokens = u.OutputTokens,
                    CachedTokens = u.CachedTokens,
                    CostUsd = u.CostUsd,
                    CostShare = u.CostShare
                };
            }).ToList();

            ByModel = byModel.Select(m => new LlmUsageByModelDto { Model = m.Model, MessageCount = m.MessageCount, InputTokens = m.InputTokens, OutputTokens = m.OutputTokens, CostUsd = m.CostUsd }).ToList();
            ByMode = byMode.Select(m => new LlmUsageByModeDto { Mode = m.Mode.ToString(), MessageCount = m.MessageCount, InputTokens = m.InputTokens, OutputTokens = m.OutputTokens, CostUsd = m.CostUsd }).ToList();
            ByDay = byDay.Select(d => new LlmUsageByDayDto { Day = d.Day, MessageCount = d.MessageCount, InputTokens = d.InputTokens, OutputTokens = d.OutputTokens, CostUsd = d.CostUsd }).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading LLM usage dashboard");
            LoadFailed = true;
        }
        finally
        {
            IsLoading = false;
            _needsLocalTimeScan = true;
        }
    }

    protected async Task SelectUserAsync(string userId)
    {
        if (SelectedUserId == userId)
        {
            SelectedUserId = null;
            return;
        }

        SelectedUserId = userId;
        await LoadDrilldownPageAsync(1);
    }

    protected async Task LoadDrilldownPageAsync(int page)
    {
        if (SelectedUserId is null || !ulong.TryParse(SelectedUserId, out var userId))
        {
            return;
        }

        var query = new LlmUsageQuery
        {
            From = _resolvedRange.From,
            To = _resolvedRange.To.AddDays(1).AddTicks(-1),
            GuildId = GuildId,
            Mode = Mode,
            UserId = userId
        };

        var paged = await UsageRepository.GetRecordsAsync(query, Math.Max(1, page), DrilldownPageSize);
        DrilldownRecords = paged.Records;
        DrilldownTotalCount = paged.TotalCount;
        DrilldownPage = Math.Max(1, page);
        _needsLocalTimeScan = true;
    }

    protected void ApplyPreset(int days)
    {
        var end = DateTime.UtcNow.Date;
        var start = end.AddDays(-days);
        NavigationManager.NavigateTo(BuildUrl(start, end));
    }

    protected void HandleFilterSubmit() => NavigationManager.NavigateTo(BuildUrl(StartInput, EndInput));

    private string BuildUrl(DateTime start, DateTime end)
    {
        var parts = new List<string>
        {
            $"StartDate={start:yyyy-MM-dd}",
            $"EndDate={end:yyyy-MM-dd}"
        };

        if (!string.IsNullOrEmpty(GuildIdInput))
        {
            parts.Add($"GuildId={GuildIdInput}");
        }

        if (!string.IsNullOrEmpty(ModeInput) && Enum.TryParse<LlmMode>(ModeInput, out var modeValue))
        {
            parts.Add($"Mode={(int)modeValue}");
        }

        return $"/Admin/LlmUsage?{string.Join('&', parts)}";
    }

    protected static string FormatTokens(long tokens) => tokens switch
    {
        >= 1_000_000 => $"{tokens / 1_000_000.0:N1}M",
        >= 1_000 => $"{tokens / 1_000.0:N1}K",
        _ => tokens.ToString("N0")
    };
}
