using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.FlaggedEvents;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/FlaggedEvents/Index.cshtml</c> +
/// <c>IndexModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d). Filters, bulk
/// select, and dismiss/acknowledge - single and bulk - all call <see cref="IFlaggedEventService"/>
/// directly (the legacy page instead had inline JS calling <c>FlaggedEventsController</c>).
/// </summary>
public partial class Index : GuildPageBase
{
    private const int DefaultPageSize = 20;

    [SupplyParameterFromQuery(Name = "FilterRuleType")]
    [Parameter]
    public int? FilterRuleTypeValue { get; set; }

    [SupplyParameterFromQuery(Name = "FilterSeverity")]
    [Parameter]
    public int? FilterSeverityValue { get; set; }

    [SupplyParameterFromQuery(Name = "FilterStatus")]
    [Parameter]
    public int? FilterStatusValue { get; set; }

    [SupplyParameterFromQuery(Name = "FilterDateFrom")]
    [Parameter]
    public DateTime? FilterDateFrom { get; set; }

    [SupplyParameterFromQuery(Name = "FilterDateTo")]
    [Parameter]
    public DateTime? FilterDateTo { get; set; }

    [SupplyParameterFromQuery(Name = "pageNumber")]
    [Parameter]
    public int? PageNumber { get; set; }

    /// <summary>Legacy <c>?page=</c> fallback query name.</summary>
    [SupplyParameterFromQuery(Name = "page")]
    [Parameter]
    public int? LegacyPage { get; set; }

    protected RuleType? FilterRuleType => (RuleType?)FilterRuleTypeValue;
    protected Severity? FilterSeverity => (Severity?)FilterSeverityValue;
    protected FlaggedEventStatus? FilterStatus => (FlaggedEventStatus?)FilterStatusValue;

    /// <summary>Effective "from" date used by the loaded query - defaults to 30 days ago when <see cref="FilterDateFrom"/> is unset, matching the legacy page.</summary>
    protected DateTime EffectiveDateFrom => FilterDateFrom ?? DateTime.Today.AddDays(-30);

    /// <summary>Effective "to" date - defaults to today when <see cref="FilterDateTo"/> is unset.</summary>
    protected DateTime EffectiveDateTo => FilterDateTo ?? DateTime.Today;

    // AuthenticationStateTask is inherited (protected) from GuildPageBase - redeclaring it here
    // as a second [CascadingParameter] throws "declares more than one parameter matching the name
    // 'authenticationstatetask'" at render time (parameter names are case-insensitive).

    [Inject]
    private IFlaggedEventService Service { get; set; } = default!;

    [Inject]
    private IServiceScopeFactory ScopeFactory { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private ILogger<Index> Logger { get; set; } = default!;

    protected IReadOnlyList<FlaggedEventDto> Events { get; private set; } = [];
    protected int TotalCount { get; private set; }
    protected bool LoadFailed { get; private set; }
    protected PagedQuery Query { get; private set; } = new(1, DefaultPageSize);
    protected int TotalPages => Query.PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / Query.PageSize) : 0;
    protected HashSet<Guid> SelectedIds { get; } = [];
    protected bool IsBusy { get; private set; }

    private (RuleType?, Severity?, FlaggedEventStatus?, DateTime?, DateTime?, int?, int?) _resolvedQuery;
    private int _loadGeneration;

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        await LoadAsync(Service);
    }

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
        await LoadAsync(Service);
        StateHasChanged();
    }

    private (RuleType?, Severity?, FlaggedEventStatus?, DateTime?, DateTime?, int?, int?) CurrentQueryKey
        => (FilterRuleType, FilterSeverity, FilterStatus, FilterDateFrom, FilterDateTo, PageNumber, LegacyPage);

    private async Task LoadAsync(IFlaggedEventService service)
    {
        var generation = ++_loadGeneration;
        LoadFailed = false;
        _resolvedQuery = CurrentQueryKey;
        Query = PagedQuery.FromQuery(PageNumber, null, defaultPageSize: DefaultPageSize, legacyPage: LegacyPage);
        SelectedIds.Clear();

        var query = new FlaggedEventQueryDto
        {
            RuleType = FilterRuleType,
            Severity = FilterSeverity,
            Status = FilterStatus,
            DateFrom = EffectiveDateFrom,
            DateTo = EffectiveDateTo,
            Page = Query.PageNumber,
            PageSize = Query.PageSize
        };

        try
        {
            var (events, totalCount) = await service.GetFilteredEventsAsync((ulong)GuildId, query);
            if (generation != _loadGeneration)
            {
                return;
            }

            Events = events.ToList();
            TotalCount = totalCount;
            RequestLocalTimeScan();
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            Logger.LogError(ex, "Failed to load flagged events for guild {GuildId}", GuildId);
            LoadFailed = true;
            Toast.Error("Failed to load flagged events.");
        }
    }

    protected bool HasActiveFilters => FilterRuleType.HasValue || FilterSeverity.HasValue || FilterStatus.HasValue;

    protected string PageUrl => $"/Guilds/FlaggedEvents/{GuildId}{FilterSuffix}";

    private string FilterSuffix
    {
        get
        {
            var parts = new List<string>();
            if (FilterRuleTypeValue.HasValue) parts.Add($"FilterRuleType={FilterRuleTypeValue}");
            if (FilterSeverityValue.HasValue) parts.Add($"FilterSeverity={FilterSeverityValue}");
            if (FilterStatusValue.HasValue) parts.Add($"FilterStatus={FilterStatusValue}");
            if (FilterDateFrom.HasValue) parts.Add($"FilterDateFrom={FilterDateFrom:yyyy-MM-dd}");
            if (FilterDateTo.HasValue) parts.Add($"FilterDateTo={FilterDateTo:yyyy-MM-dd}");
            return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
        }
    }

    protected string DetailsUrl(Guid id) => $"/Guilds/FlaggedEvents/{GuildId}/{id}";

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

    protected void ToggleSelectAll(bool selected)
    {
        SelectedIds.Clear();
        if (selected)
        {
            foreach (var evt in Events)
            {
                SelectedIds.Add(evt.Id);
            }
        }
    }

    protected void ClearSelection() => SelectedIds.Clear();

    /// <summary>
    /// Resolves the reviewer id from the current user's linked Discord account
    /// (<c>discord:user_id</c> claim, via <see cref="ClaimsPrincipalExtensions.GetDiscordUserId"/>)
    /// - the same claim <c>Blazor/Pages/Guilds/FeatureRequests/Details.razor.cs</c> already uses
    /// for the same purpose. The legacy inline JS instead read a claim named "DiscordId", which no
    /// authentication handler in this app ever issues - that value was always empty in production,
    /// so a dismiss/acknowledge/action POST always sent an unparsable reviewerId and (assuming the
    /// request even reached the controller) would have failed model binding. Ported forward with
    /// the fix: resolve the real claim, and refuse the action with a toast when it's absent instead
    /// of sending an id known to be wrong.
    /// </summary>
    private async Task<ulong?> GetReviewerIdAsync()
    {
        if (AuthenticationStateTask is null)
        {
            return null;
        }

        var user = (await AuthenticationStateTask).User;
        var id = user.GetDiscordUserId();
        return id == 0 ? null : id;
    }

    protected async Task HandleDismissAsync(Guid id)
    {
        var reviewerId = await GetReviewerIdAsync();
        if (reviewerId is null)
        {
            Toast.Error("Your account isn't linked to a Discord user, so this action can't be recorded.");
            return;
        }

        IsBusy = true;
        try
        {
            await ScopeFactory.RunAsync<IFlaggedEventService>(s => s.DismissEventAsync(id, reviewerId.Value));
            Toast.Success("Event dismissed.");
            SelectedIds.Remove(id);
            await ScopeFactory.RunAsync<IFlaggedEventService>(LoadAsync);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error dismissing flagged event {EventId}", id);
            Toast.Error("An error occurred while dismissing the event.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected async Task HandleBulkDismissAsync() => await HandleBulkAsync(
        (service, id, reviewerId) => service.DismissEventAsync(id, reviewerId), "dismissed");

    protected async Task HandleBulkAcknowledgeAsync() => await HandleBulkAsync(
        (service, id, reviewerId) => service.AcknowledgeEventAsync(id, reviewerId), "acknowledged");

    private async Task HandleBulkAsync(Func<IFlaggedEventService, Guid, ulong, Task<FlaggedEventDto?>> action, string verb)
    {
        if (SelectedIds.Count == 0)
        {
            return;
        }

        var reviewerId = await GetReviewerIdAsync();
        if (reviewerId is null)
        {
            Toast.Error("Your account isn't linked to a Discord user, so this action can't be recorded.");
            return;
        }

        var ids = SelectedIds.ToList();
        IsBusy = true;
        try
        {
            await ScopeFactory.RunAsync<IFlaggedEventService>(async service =>
            {
                foreach (var id in ids)
                {
                    await action(service, id, reviewerId.Value);
                }
            });

            Toast.Success($"{ids.Count} event{(ids.Count == 1 ? "" : "s")} {verb}.");
            SelectedIds.Clear();
            await ScopeFactory.RunAsync<IFlaggedEventService>(LoadAsync);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error bulk-{Verb} flagged events", verb);
            Toast.Error($"An error occurred while marking events as {verb}.");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
