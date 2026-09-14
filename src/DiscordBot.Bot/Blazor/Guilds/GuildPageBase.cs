using System.Security.Claims;
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

    /// <summary>The full result of resolving this route's guild id - null before the first resolution completes.</summary>
    protected GuildContextResult? Result { get; private set; }

    /// <summary>Shorthand for <c>Result?.Context</c> - non-null only when <c>Result.Status == Ok</c>.</summary>
    protected GuildContext? Guild => Result?.Context;

    /// <summary>True while the initial (or a guild-id-changed) resolution is in flight.</summary>
    protected bool IsLoading { get; private set; } = true;

    private PersistingComponentStateSubscription _persistingSubscription;
    private ulong? _resolvedGuildId;

    private string PersistenceKey => $"GuildPageBase.GuildContext.{(ulong)GuildId}";

    protected sealed override async Task OnInitializedAsync()
    {
        _persistingSubscription = ApplicationState.RegisterOnPersisting(PersistCurrentResultAsync);

        if (ApplicationState.TryTakeFromJson<GuildContextResult>(PersistenceKey, out var restored) && restored is not null)
        {
            Result = restored;
            IsLoading = false;
            _resolvedGuildId = (ulong)GuildId;
        }
        else
        {
            await ResolveAsync();
        }

        await OnGuildContextReadyAsync();
    }

    protected sealed override async Task OnParametersSetAsync()
    {
        if (_resolvedGuildId != (ulong)GuildId)
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

    private async Task ResolveAsync()
    {
        IsLoading = true;
        var guildId = (ulong)GuildId;
        var user = AuthenticationStateTask is not null
            ? (await AuthenticationStateTask).User
            : new ClaimsPrincipal(new ClaimsIdentity());

        Result = await ContextProvider.GetAsync(guildId, user);
        _resolvedGuildId = guildId;
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
