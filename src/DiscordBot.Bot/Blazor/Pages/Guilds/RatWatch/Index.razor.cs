using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.RatWatch;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/RatWatch/Index.cshtml</c> +
/// <c>IndexModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b). Reuses the legacy
/// <see cref="RatWatchItemViewModel"/>/<see cref="RatLeaderboardEntryViewModel"/> (still alive -
/// <c>Pages/Guilds/RatWatch/Incidents.cshtml.cs</c> also depends on them) for their status/badge
/// derivation instead of reimplementing it, since neither view model is Razor Pages-specific.
/// </summary>
/// <remarks>
/// Cancel/end-vote/settings-update are component methods instead of the legacy page's three POST
/// handlers, with the same validation rules (timezone non-blank, hours 1-168, voting 1-60) and
/// redirect-back-to-Index semantics reproduced as "reload in place" (there is nowhere else to
/// redirect to - this already is Index).
/// </remarks>
/// <remarks>
/// Pagination is standardised on <see cref="PagedQuery"/> + link-mode <c>Pagination</c>, matching
/// <c>FeatureRequests/Index.razor.cs</c>/<c>Reminders/Index.razor.cs</c> (docs/architecture/patterns.md,
/// "Paged list pages") rather than hand-rolling page state behind a one-time <c>_seeded</c> flag
/// and a callback-mode <c>Pagination</c> that changed the URL without ever reloading for it - so
/// browser back/forward across a page change here now reloads like every other paged guild list.
/// </remarks>
public partial class Index : GuildPageBase
{
    private const int PageSize = 20;

    [SupplyParameterFromQuery(Name = "pageNumber")]
    [Parameter]
    public int? PageNumber { get; set; }

    [Inject]
    private IRatWatchService RatWatchService { get; set; } = default!;

    [Inject]
    private IRatWatchRepository RatWatchRepository { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private ILogger<Index> Logger { get; set; } = default!;

    protected GuildRatWatchSettings Settings { get; private set; } = new();
    protected IReadOnlyList<RatWatchItemViewModel> Watches { get; private set; } = [];
    protected IReadOnlyList<RatLeaderboardEntryViewModel> Leaderboard { get; private set; } = [];
    protected RatWatchAnalyticsSummaryDto? AnalyticsSummary { get; private set; }
    protected int TotalWatches { get; private set; }
    protected bool LoadFailed { get; private set; }
    protected PagedQuery Query { get; private set; } = new(1, PageSize);
    protected int TotalPages => Query.PageSize > 0 ? (int)Math.Ceiling((double)TotalWatches / Query.PageSize) : 0;
    protected string PageUrl => $"/Guilds/RatWatch/{GuildId}";

    protected int PendingCount => Watches.Count(w => w.Status == RatWatchStatus.Pending);
    protected int VotingCount => Watches.Count(w => w.Status == RatWatchStatus.Voting);
    protected int CompletedCount => Watches.Count(w => w.Status is RatWatchStatus.Guilty or RatWatchStatus.NotGuilty);

    protected bool IsEditingSettings { get; set; }
    protected SettingsInputModel SettingsInput { get; set; } = new();
    protected string? SettingsError { get; set; }

    private ConfirmModal? _cancelModal;
    private ConfirmModal? _endVoteModal;
    private RatWatchItemViewModel? _pendingWatch;

    private int? _resolvedPageNumber;

    /// <summary>See the identical note on <c>FeatureRequests/Index.razor.cs</c>'s field of the same name.</summary>
    private int _loadGeneration;

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        await LoadAsync(RatWatchService, RatWatchRepository);
    }

    /// <summary>See the identical note on <c>FeatureRequests/Index.razor.cs</c>.</summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (Guild is not null && _resolvedPageNumber != PageNumber)
        {
            _ = InvokeAsync(ReloadAndRerenderAsync);
        }
    }

    private async Task ReloadAndRerenderAsync()
    {
        await LoadAsync(RatWatchService, RatWatchRepository);
        StateHasChanged();
    }

    /// <summary>
    /// Loads the page's data through <paramref name="ratWatchService"/>/<paramref name="ratWatchRepository"/>
    /// - the circuit-scoped <see cref="RatWatchService"/>/<see cref="RatWatchRepository"/> for the
    /// initial and page-change loads, a fresh scope's instances via <see cref="ScopeFactory"/> for
    /// the reload that follows a settings-save/cancel/end-vote mutation (docs/architecture/patterns.md
    /// "Blazor Components" § Per-operation scopes).
    /// </summary>
    private async Task LoadAsync(IRatWatchService ratWatchService, IRatWatchRepository ratWatchRepository)
    {
        var generation = ++_loadGeneration;
        LoadFailed = false;
        _resolvedPageNumber = PageNumber;
        Query = PagedQuery.FromQuery(PageNumber, null, defaultPageSize: PageSize);

        var guildId = (ulong)GuildId;

        try
        {
            var settings = await ratWatchService.GetGuildSettingsAsync(guildId);
            var (watches, totalCount) = await ratWatchService.GetByGuildAsync(guildId, Query.PageNumber, Query.PageSize);
            var leaderboard = await ratWatchService.GetLeaderboardAsync(guildId, 10);
            var analyticsSummary = await ratWatchRepository.GetAnalyticsSummaryAsync(guildId, null, null);

            if (generation != _loadGeneration)
            {
                // A newer load (another page change, or a settings/cancel/end-vote reload) already
                // superseded this one - its result wins.
                return;
            }

            Settings = settings;
            SeedSettingsInput();
            Watches = watches.Select(w => RatWatchItemViewModel.FromDto(w, Settings.VotingDurationMinutes)).ToList();
            TotalWatches = totalCount;
            Leaderboard = leaderboard.Select(RatLeaderboardEntryViewModel.FromDto).ToList();
            AnalyticsSummary = analyticsSummary;
            RequestLocalTimeScan();

            Logger.LogDebug("Retrieved {Count} watches for guild {GuildId} (page {Page} of {TotalPages})",
                Watches.Count, guildId, Query.PageNumber, TotalPages);
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            Logger.LogError(ex, "Failed to load Rat Watch data for guild {GuildId}", guildId);
            LoadFailed = true;
            Toast.Error("Failed to load Rat Watch data.");
        }
    }

    private void SeedSettingsInput()
    {
        SettingsInput = new SettingsInputModel
        {
            Timezone = Settings.Timezone,
            MaxAdvanceHours = Settings.MaxAdvanceHours,
            VotingDurationMinutes = Settings.VotingDurationMinutes,
            IsEnabled = Settings.IsEnabled,
            PublicLeaderboardEnabled = Settings.PublicLeaderboardEnabled
        };
    }

    protected void EnterEditSettings()
    {
        SeedSettingsInput();
        SettingsError = null;
        IsEditingSettings = true;
    }

    protected void CancelEditSettings()
    {
        IsEditingSettings = false;
        SettingsError = null;
    }

    protected async Task HandleUpdateSettings()
    {
        if (string.IsNullOrWhiteSpace(SettingsInput.Timezone))
        {
            SettingsError = "Timezone is required.";
            return;
        }

        if (SettingsInput.MaxAdvanceHours is < 1 or > 168)
        {
            SettingsError = "Max advance hours must be between 1 and 168 (1 week).";
            return;
        }

        if (SettingsInput.VotingDurationMinutes is < 1 or > 60)
        {
            SettingsError = "Voting duration must be between 1 and 60 minutes.";
            return;
        }

        var guildId = (ulong)GuildId;
        try
        {
            await ScopeFactory.RunAsync<IRatWatchService, GuildRatWatchSettings>(s => s.UpdateGuildSettingsAsync(guildId, settings =>
            {
                settings.Timezone = SettingsInput.Timezone;
                settings.MaxAdvanceHours = SettingsInput.MaxAdvanceHours;
                settings.VotingDurationMinutes = SettingsInput.VotingDurationMinutes;
                settings.IsEnabled = SettingsInput.IsEnabled;
                settings.PublicLeaderboardEnabled = SettingsInput.PublicLeaderboardEnabled;
            }));

            Logger.LogInformation("Successfully updated Rat Watch settings for guild {GuildId}", guildId);
            Toast.Success("Rat Watch settings updated successfully.");
            SettingsError = null;
            IsEditingSettings = false;
            await ScopeFactory.RunAsync<IRatWatchService, IRatWatchRepository>(LoadAsync);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to update Rat Watch settings for guild {GuildId}", guildId);
            SettingsError = "Failed to update settings. Please try again.";
        }
    }

    protected async Task RequestCancel(RatWatchItemViewModel watch)
    {
        _pendingWatch = watch;
        if (_cancelModal is not null && await _cancelModal.ShowAsync())
        {
            await ConfirmCancelAsync();
        }
    }

    private async Task ConfirmCancelAsync()
    {
        if (_pendingWatch is null)
        {
            return;
        }

        var watchId = _pendingWatch.Id;
        var success = await ScopeFactory.RunAsync<IRatWatchService, bool>(s => s.CancelWatchAsync(watchId, "Cancelled by administrator from Admin UI"));

        if (success)
        {
            Logger.LogInformation("Successfully cancelled Rat Watch {WatchId}", watchId);
            Toast.Success("Rat Watch cancelled successfully.");
        }
        else
        {
            Logger.LogWarning("Failed to cancel Rat Watch {WatchId} - not found or already completed", watchId);
            Toast.Error("Could not cancel the Rat Watch. It may have already completed or been cancelled.");
        }

        _pendingWatch = null;
        await ScopeFactory.RunAsync<IRatWatchService, IRatWatchRepository>(LoadAsync);
    }

    protected async Task RequestEndVote(RatWatchItemViewModel watch)
    {
        _pendingWatch = watch;
        if (_endVoteModal is not null && await _endVoteModal.ShowAsync())
        {
            await ConfirmEndVoteAsync();
        }
    }

    private async Task ConfirmEndVoteAsync()
    {
        if (_pendingWatch is null)
        {
            return;
        }

        var watchId = _pendingWatch.Id;
        var success = await ScopeFactory.RunAsync<IRatWatchService, bool>(s => s.FinalizeVotingAsync(watchId));

        if (success)
        {
            Logger.LogInformation("Successfully ended voting on Rat Watch {WatchId}", watchId);
            Toast.Success("Voting ended and verdict determined.");
        }
        else
        {
            Logger.LogWarning("Failed to end voting on Rat Watch {WatchId} - not found or not in voting status", watchId);
            Toast.Error("Could not end voting. The watch may not be in voting status.");
        }

        _pendingWatch = null;
        await ScopeFactory.RunAsync<IRatWatchService, IRatWatchRepository>(LoadAsync);
    }

    protected string CancelModalMessage => _pendingWatch is null
        ? string.Empty
        : $"Are you sure you want to cancel the watch for {_pendingWatch.AccusedUsername}? This action cannot be undone.";

    protected string EndVoteModalMessage => _pendingWatch is null
        ? string.Empty
        : $"Are you sure you want to end voting for {_pendingWatch.AccusedUsername}? Current tally: {_pendingWatch.GuiltyVotes} Guilty, {_pendingWatch.NotGuiltyVotes} Not Guilty. The verdict will be determined based on current votes.";

    /// <summary>Local status→style mapping for the "Recent Watches" table's inline pill (the
    /// legacy page's inline switch expressions) - not shared with any other ported page.</summary>
    protected static (string BackgroundText, string Dot) StatusPillClasses(RatWatchStatus status) => status switch
    {
        RatWatchStatus.Pending => ("bg-warning/20 text-warning", "bg-warning"),
        RatWatchStatus.Voting => ("bg-accent-blue/20 text-accent-blue", "bg-accent-blue"),
        RatWatchStatus.Guilty => ("bg-error/20 text-error", "bg-error"),
        RatWatchStatus.NotGuilty => ("bg-success/20 text-success", "bg-success"),
        RatWatchStatus.ClearedEarly => ("bg-bg-tertiary text-text-secondary", "bg-text-secondary"),
        RatWatchStatus.Expired => ("bg-bg-tertiary text-text-tertiary", "bg-text-tertiary"),
        RatWatchStatus.Cancelled => ("bg-error/20 text-error", "bg-error"),
        _ => ("bg-bg-tertiary text-text-tertiary", "bg-text-tertiary")
    };

    public sealed class SettingsInputModel
    {
        public string Timezone { get; set; } = "Eastern Standard Time";
        public int MaxAdvanceHours { get; set; } = 24;
        public int VotingDurationMinutes { get; set; } = 5;
        public bool IsEnabled { get; set; } = true;
        public bool PublicLeaderboardEnabled { get; set; }
    }
}
