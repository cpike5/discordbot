using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.Services.Reminders;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.Reminders;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/Reminders/Index.cshtml</c> +
/// <c>IndexModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b). Reproduces the
/// stats cards, status filter, per-row Discord user resolution (via <see cref="IReminderUserResolver"/>
/// instead of a raw <see cref="Discord.WebSocket.DiscordSocketClient"/> lookup so this is testable),
/// and the Pending-only cancel confirmation.
/// </summary>
public partial class Index : GuildPageBase
{
    private const int PageSize = 20;

    /// <summary>
    /// Raw query value backing <see cref="Status"/> - <c>[SupplyParameterFromQuery]</c> cannot bind
    /// a nullable enum directly (see the identical note on
    /// <c>FeatureRequests/Index.razor.cs</c>'s <c>StatusFilterValue</c>), so the query is bound as
    /// <c>int?</c> and converted here.
    /// </summary>
    [SupplyParameterFromQuery(Name = "status")]
    [Parameter]
    public int? StatusValue { get; set; }

    protected ReminderStatus? Status => (ReminderStatus?)StatusValue;

    [SupplyParameterFromQuery(Name = "pageNumber")]
    [Parameter]
    public int? PageNumber { get; set; }

    /// <summary>Legacy <c>?page=</c> fallback query name, used only when <see cref="PageNumber"/> is absent.</summary>
    [SupplyParameterFromQuery(Name = "page")]
    [Parameter]
    public int? LegacyPage { get; set; }

    [Inject]
    private IReminderRepository ReminderRepository { get; set; } = default!;

    [Inject]
    private IReminderUserResolver UserResolver { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    private ILogger<Index> Logger { get; set; } = default!;

    protected IReadOnlyList<ReminderRow> Rows { get; private set; } = [];
    protected int TotalCount { get; private set; }
    protected bool LoadFailed { get; private set; }
    protected PagedQuery Query { get; private set; } = new(1, PageSize);
    protected int TotalPages => Query.PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / Query.PageSize) : 0;

    protected int StatTotal { get; private set; }
    protected int StatPending { get; private set; }
    protected int StatDeliveredToday { get; private set; }
    protected int StatFailed { get; private set; }

    private ConfirmModal? _cancelModal;
    private ReminderRow? _pendingCancel;
    private (ReminderStatus?, int?, int?) _resolvedQuery;

    /// <summary>See the identical note on <c>FeatureRequests/Index.razor.cs</c>'s field of the same name.</summary>
    private int _loadGeneration;

    protected string ToggleModalMessage => _pendingCancel is null
        ? string.Empty
        : $"Are you sure you want to cancel the reminder for {_pendingCancel.Username}? This action cannot be undone.";

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        await LoadAsync(ReminderRepository);
    }

    /// <summary>See the identical note on <c>FeatureRequests/Index.razor.cs</c>.</summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (Guild is not null && _resolvedQuery != CurrentQueryKey)
        {
            _ = InvokeAsync(ReloadAndRerenderAsync);
        }
    }

    private async Task ReloadAndRerenderAsync()
    {
        await LoadAsync(ReminderRepository);
        StateHasChanged();
    }

    private (ReminderStatus?, int?, int?) CurrentQueryKey => (Status, PageNumber, LegacyPage);

    /// <summary>
    /// Loads the page's data through <paramref name="reminderRepository"/> - the circuit-scoped
    /// <see cref="ReminderRepository"/> for the initial and query-driven loads, a fresh scope's
    /// instance via <see cref="ScopeFactory"/> for the reload that follows
    /// <see cref="ConfirmCancelAsync"/>'s mutation (docs/architecture/patterns.md "Blazor
    /// Components" § Per-operation scopes). <see cref="UserResolver"/> stays circuit-scoped in
    /// both cases - it resolves Discord user info, not <c>BotDbContext</c> state, so it carries
    /// none of the stale-tracking risk a repository does.
    /// </summary>
    private async Task LoadAsync(IReminderRepository reminderRepository)
    {
        var generation = ++_loadGeneration;
        LoadFailed = false;
        _resolvedQuery = CurrentQueryKey;
        Query = PagedQuery.FromQuery(PageNumber, null, defaultPageSize: PageSize, legacyPage: LegacyPage);

        var guildId = (ulong)GuildId;

        try
        {
            var (reminders, total) = await reminderRepository.GetByGuildAsync(guildId, Query.PageNumber, Query.PageSize, Status);
            var (statTotal, statPending, statDeliveredToday, statFailed) = await reminderRepository.GetGuildStatsAsync(guildId);

            var rows = new List<ReminderRow>();
            foreach (var reminder in reminders)
            {
                var userInfo = await UserResolver.ResolveAsync(guildId, reminder.UserId);
                rows.Add(new ReminderRow(reminder, userInfo.Username, userInfo.AvatarUrl));
            }

            if (generation != _loadGeneration)
            {
                // A newer load (another query-string change, or a cancel-then-reload) already
                // superseded this one - its result wins.
                return;
            }

            TotalCount = total;
            StatTotal = statTotal;
            StatPending = statPending;
            StatDeliveredToday = statDeliveredToday;
            StatFailed = statFailed;
            Rows = rows;
            RequestLocalTimeScan();
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            Logger.LogError(ex, "Failed to load reminders for guild {GuildId}", guildId);
            LoadFailed = true;
            Toast.Error("Failed to load reminders.");
        }
    }

    protected string PageUrl => $"/Guilds/Reminders/{GuildId}{FilterSuffix}";

    private string FilterSuffix => Status.HasValue ? $"?status={(int)Status.Value}" : string.Empty;

    protected void HandleStatusChanged(ChangeEventArgs e)
    {
        var raw = e.Value?.ToString();
        var query = string.IsNullOrEmpty(raw) ? string.Empty : $"?status={raw}";
        NavigationManager.NavigateTo($"/Guilds/Reminders/{GuildId}{query}");
    }

    protected async Task RequestCancel(ReminderRow row)
    {
        _pendingCancel = row;
        var confirmed = _cancelModal is not null && await _cancelModal.ShowAsync();
        if (confirmed)
        {
            await ConfirmCancelAsync();
        }
    }

    private async Task ConfirmCancelAsync()
    {
        if (_pendingCancel is null)
        {
            return;
        }

        var pendingCancelId = _pendingCancel.Reminder.Id;
        var outcome = await ScopeFactory.RunAsync<IReminderRepository, CancelOutcome>(async repo =>
        {
            var reminder = await repo.GetByIdAsync(pendingCancelId);
            if (reminder is null || reminder.GuildId != (ulong)GuildId)
            {
                return CancelOutcome.NotFound;
            }

            if (reminder.Status != ReminderStatus.Pending)
            {
                return CancelOutcome.NotPending;
            }

            reminder.Status = ReminderStatus.Cancelled;
            await repo.UpdateAsync(reminder);
            return CancelOutcome.Cancelled;
        });

        switch (outcome)
        {
            case CancelOutcome.NotFound:
                Toast.Error("Reminder not found.");
                break;
            case CancelOutcome.NotPending:
                Toast.Error("Only pending reminders can be cancelled.");
                break;
            case CancelOutcome.Cancelled:
                Toast.Success("Reminder cancelled successfully.");
                break;
        }

        _pendingCancel = null;

        if (outcome == CancelOutcome.Cancelled)
        {
            await ScopeFactory.RunAsync<IReminderRepository>(LoadAsync);
        }
    }

    private enum CancelOutcome
    {
        NotFound,
        NotPending,
        Cancelled
    }

    /// <summary>Display row pairing a <see cref="Reminder"/> with its resolved Discord username/avatar.</summary>
    public sealed record ReminderRow(Reminder Reminder, string Username, string? AvatarUrl)
    {
        public Guid Id => Reminder.Id;
        public string Message => Reminder.Message;
        public string MessagePreview => Message.Length > 50 ? Message[..50] + "..." : Message;
        public DateTime TriggerAt => Reminder.TriggerAt;
        public DateTime CreatedAt => Reminder.CreatedAt;
        public ReminderStatus Status => Reminder.Status;
        public string? LastError => Reminder.LastError;
        public bool CanCancel => Status == ReminderStatus.Pending;
        public ulong UserId => Reminder.UserId;

        public string StatusText => Status switch
        {
            ReminderStatus.Pending => "Pending",
            ReminderStatus.Delivered => "Delivered",
            ReminderStatus.Failed => "Failed",
            ReminderStatus.Cancelled => "Cancelled",
            _ => "Unknown"
        };
    }
}
