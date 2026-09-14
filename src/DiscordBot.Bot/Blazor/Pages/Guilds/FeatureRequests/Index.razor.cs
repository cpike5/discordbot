using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Pages.Guilds.FeatureRequests;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/FeatureRequests/Index.cshtml</c> +
/// <c>IndexModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b). Loads once per
/// distinct <see cref="StatusFilter"/>/page in <see cref="OnGuildContextReadyAsync"/>.
/// </summary>
public partial class Index : GuildPageBase
{
    private const int PageSize = 20;

    /// <summary>
    /// Raw query value backing <see cref="StatusFilter"/>. <c>[SupplyParameterFromQuery]</c> only
    /// supports a fixed set of primitive types (string, bool, DateTime, decimal, double, float,
    /// Guid, int, long, and arrays of those) - a nullable enum throws
    /// <c>InvalidOperationException</c> ("Querystring values cannot be parsed as type ...") at
    /// render time, so the query is bound as the enum's underlying <c>int?</c> and converted here.
    /// </summary>
    [SupplyParameterFromQuery(Name = "StatusFilter")]
    [Parameter]
    public int? StatusFilterValue { get; set; }

    protected FeatureRequestStatus? StatusFilter => (FeatureRequestStatus?)StatusFilterValue;

    [SupplyParameterFromQuery(Name = "pageNumber")]
    [Parameter]
    public int? PageNumber { get; set; }

    /// <summary>Legacy <c>?page=</c> fallback query name, used only when <see cref="PageNumber"/> is absent.</summary>
    [SupplyParameterFromQuery(Name = "page")]
    [Parameter]
    public int? LegacyPage { get; set; }

    [Inject]
    private IFeatureRequestService Service { get; set; } = default!;

    [Inject]
    private IToastService Toast { get; set; } = default!;

    [Inject]
    private ILogger<Index> Logger { get; set; } = default!;

    protected IReadOnlyList<FeatureRequest> Items { get; private set; } = [];
    protected int Total { get; private set; }
    protected bool IsLoadingItems { get; private set; } = true;
    protected bool LoadFailed { get; private set; }
    protected PagedQuery Query { get; private set; } = new(1, PageSize);
    protected int TotalPages => Query.PageSize > 0 ? (int)Math.Ceiling((double)Total / Query.PageSize) : 0;

    private (FeatureRequestStatus?, int?, int?) _resolvedQuery;

    /// <summary>
    /// Incremented at the start of every <see cref="LoadAsync"/> so a superseded load (two
    /// query-string changes in quick succession) doesn't clobber a later one's results with its
    /// own once it finally finishes - see <see cref="LoadAsync"/>.
    /// </summary>
    private int _loadGeneration;

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        await LoadAsync();
    }

    /// <summary>
    /// <see cref="GuildPageBase"/> only re-resolves (and calls <see cref="OnGuildContextReadyAsync"/>
    /// again) when <c>GuildId</c> itself changes - a query-string-only change (a new
    /// <see cref="StatusFilter"/> or page, the same guild) leaves its sealed
    /// <c>OnParametersSetAsync</c> a no-op. This unsealed synchronous hook is the one place left to
    /// notice that and kick off a reload. It dispatches through <see cref="ComponentBase.InvokeAsync(Action)"/>
    /// rather than a bare fire-and-forget task, so a two-query-changes-in-a-row race can't leave an
    /// unobserved exception (<see cref="LoadAsync"/> itself catches and surfaces one anyway - see
    /// there) and the reload runs on the renderer's synchronization context like every other
    /// UI-driven call; <c>Blazor/Pages/Admin/Users/Index.razor.cs</c> avoids needing this only
    /// because it isn't guild-scoped and can safely override the async hook directly.
    /// </summary>
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
        await LoadAsync();
        StateHasChanged();
    }

    private (FeatureRequestStatus?, int?, int?) CurrentQueryKey => (StatusFilter, PageNumber, LegacyPage);

    private async Task LoadAsync()
    {
        var generation = ++_loadGeneration;
        IsLoadingItems = true;
        LoadFailed = false;
        _resolvedQuery = CurrentQueryKey;
        Query = PagedQuery.FromQuery(PageNumber, null, defaultPageSize: PageSize, legacyPage: LegacyPage);

        try
        {
            var (items, total) = await Service.GetByGuildIdAsync((ulong)GuildId, StatusFilter, Query.PageNumber, Query.PageSize);
            if (generation != _loadGeneration)
            {
                // A newer load has already started (another query-string change landed while this
                // one was in flight) - its result wins, not this stale one.
                return;
            }

            Items = items.ToList();
            Total = total;
            RequestLocalTimeScan();
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            Logger.LogError(ex, "Failed to load feature requests for guild {GuildId}", GuildId);
            LoadFailed = true;
            Toast.Error("Failed to load feature requests.");
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                IsLoadingItems = false;
            }
        }
    }

    protected string PageUrl => $"/Guilds/FeatureRequests/{GuildId}{FilterSuffix}";

    private string FilterSuffix => StatusFilter.HasValue ? $"?StatusFilter={(int)StatusFilter.Value}" : string.Empty;

    protected string DetailsUrl(Guid id) => $"/Guilds/FeatureRequests/{GuildId}/{id}";

    protected static string ShortId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    protected static string Truncate(string text, int maxLength)
        => text.Length > maxLength ? text[..maxLength] + "..." : text;
}
