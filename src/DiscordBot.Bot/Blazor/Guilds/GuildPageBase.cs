using System.Security.Claims;
using DiscordBot.Bot.Blazor.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DiscordBot.Bot.Blazor.Guilds;

/// <summary>
/// Base class for a routable guild page. Resolves this route's <see cref="GuildContext"/> once
/// via <see cref="IGuildContextProvider"/> (memoised, so <c>GuildLayout</c> resolving the same
/// guild id costs nothing extra) and persists it across the prerender-to-circuit boundary with
/// <see cref="PersistentComponentState"/>, so the provider - and everything it calls - runs once
/// per page load, not twice. See "GuildContext" in <c>docs/architecture/patterns.md</c>.
/// </summary>
/// <remarks>
/// A derived page overrides <see cref="OnGuildContextReadyAsync"/> for its own data loading
/// instead of <c>OnInitializedAsync</c>/<c>OnParametersSetAsync</c> - both are sealed here so the
/// resolve-once-and-persist bookkeeping can't be bypassed by accident. The hook runs after every
/// resolution, including a failed one (<see cref="Result"/> is <c>NotFound</c>/<c>Forbidden</c>),
/// since some pages may want to react to that too; most will just guard on <see cref="Guild"/>
/// being non-null.
/// </remarks>
public abstract class GuildPageBase : ComponentBase, IDisposable
{
    /// <summary>
    /// The guild id from the route. <c>long</c>, not <c>ulong</c>, because ASP.NET routing has no
    /// <c>ulong</c> route-constraint type - guild routes are declared <c>{guildId:long}</c> (see
    /// the Facts note in the Phase 3 port brief); cast to <c>ulong</c> internally wherever it
    /// reaches a guild id.
    /// </summary>
    [Parameter]
    public long GuildId { get; set; }

    [CascadingParameter]
    protected Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private IGuildContextProvider ContextProvider { get; set; } = default!;

    [Inject]
    private PersistentComponentState ApplicationState { get; set; } = default!;

    [Inject]
    private BrowserInterop BrowserInterop { get; set; } = default!;

    /// <summary>The full result of resolving this route's guild id - null before the first resolution completes.</summary>
    protected GuildContextResult? Result { get; private set; }

    /// <summary>Shorthand for <c>Result?.Context</c> - non-null only when <c>Result.Status == Ok</c>.</summary>
    protected GuildContext? Guild => Result?.Context;

    /// <summary>True while the initial (or a guild-id-changed) resolution is in flight.</summary>
    protected bool IsLoading { get; private set; } = true;

    private PersistingComponentStateSubscription _persistingSubscription;

    // long, not ulong, for the same reason GuildId itself is - see ResolveAsync for where (and
    // why) the ulong cast actually happens.
    private long? _resolvedGuildId;

    /// <summary>
    /// True once a derived page has called <see cref="RequestLocalTimeScan"/> after loading or
    /// re-rendering rows containing <c>&lt;LocalTime&gt;</c> - consumed by the next
    /// <see cref="OnAfterRenderAsync"/>, which the document-level scan in <c>localtime.js</c> never
    /// re-fires for on its own past the very first render. Same pattern as
    /// <c>Blazor/Pages/Admin/Users/Index.razor.cs</c>/<c>Blazor/Pages/Search.razor.cs</c>, lifted
    /// here (docs/architecture/patterns.md, "GuildContext") so every guild list/detail page gets it
    /// for free instead of reimplementing the flag and the <c>OnAfterRenderAsync</c> override.
    /// </summary>
    private bool _needsLocalTimeScan;

    private string PersistenceKey => $"GuildPageBase.GuildContext.{GuildId}";

    protected sealed override async Task OnInitializedAsync()
    {
        _persistingSubscription = ApplicationState.RegisterOnPersisting(PersistCurrentResultAsync);

        if (ApplicationState.TryTakeFromJson<GuildContextResult>(PersistenceKey, out var restored) && restored is not null)
        {
            Result = restored;
            IsLoading = false;
            _resolvedGuildId = GuildId;
        }
        else
        {
            await ResolveAsync();
        }

        await OnGuildContextReadyAsync();
    }

    protected sealed override async Task OnParametersSetAsync()
    {
        if (_resolvedGuildId != GuildId)
        {
            await ResolveAsync();
            await OnGuildContextReadyAsync();
        }
    }

    /// <summary>
    /// Called once after every (re)resolution of <see cref="Result"/> - override this instead of
    /// <c>OnInitializedAsync</c>/<c>OnParametersSetAsync</c> for a page's own data loading, which
    /// typically depends on <see cref="Guild"/> being available.
    /// </summary>
    protected virtual Task OnGuildContextReadyAsync() => Task.CompletedTask;

    /// <summary>
    /// Call after loading or re-rendering rows containing <c>&lt;LocalTime&gt;</c> (initial load,
    /// a page/filter change, an action that reloads the list) so the next render pass re-triggers
    /// the browser-side local-time conversion. Not called automatically - a page that renders
    /// <c>&lt;LocalTime&gt;</c> and can re-render its data after the first load must call this at
    /// the end of every load/reload.
    /// </summary>
    protected void RequestLocalTimeScan() => _needsLocalTimeScan = true;

    /// <summary>
    /// Runs the local-time scan requested by <see cref="RequestLocalTimeScan"/>, if any. Virtual
    /// (not sealed, unlike the lifecycle pair above) so a page with its own post-render work (e.g.
    /// <c>ScheduledMessages/Edit.razor.cs</c> detecting the viewer's timezone) can override it and
    /// call <c>base.OnAfterRenderAsync(firstRender)</c> alongside its own logic.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_needsLocalTimeScan)
        {
            _needsLocalTimeScan = false;
            await BrowserInterop.ConvertLocalTimesAsync();
        }
    }

    private async Task ResolveAsync()
    {
        IsLoading = true;
        _resolvedGuildId = GuildId;

        if (GuildId <= 0)
        {
            // {guildId:long} accepts zero and negative values, which GuildRoutes.TryGetGuildId's
            // \d+ regex never matches - GuildLayout then renders no chrome for the exact same
            // URL this page treats as a route. Fail this out as NotFound directly instead of
            // unchecked-casting a negative/zero long to a huge, meaningless ulong and asking
            // IGuildContextProvider to look that up.
            Result = GuildContextResult.NotFound();
            IsLoading = false;
            return;
        }

        var guildId = (ulong)GuildId;
        var user = AuthenticationStateTask is not null
            ? (await AuthenticationStateTask).User
            : new ClaimsPrincipal(new ClaimsIdentity());

        Result = await ContextProvider.GetAsync(guildId, user);
        IsLoading = false;
    }

    private Task PersistCurrentResultAsync()
    {
        if (Result is not null)
        {
            ApplicationState.PersistAsJson(PersistenceKey, Result);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _persistingSubscription.Dispose();
        GC.SuppressFinalize(this);
    }
}
