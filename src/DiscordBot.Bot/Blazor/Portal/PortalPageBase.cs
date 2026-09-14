using System.Security.Claims;
using DiscordBot.Bot.Services.Portal;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace DiscordBot.Bot.Blazor.Portal;

/// <summary>
/// Base class for a routable Portal page - the Portal counterpart to
/// <c>DiscordBot.Bot.Blazor.Guilds.GuildPageBase</c>, same resolve-once-and-persist shape (see
/// that type's remarks and "GuildContext"/"Portal three-state gate" in
/// <c>docs/architecture/patterns.md</c>), built on <see cref="IPortalContextProvider"/> instead of
/// <c>IGuildContextProvider</c>. A Portal page is reachable anonymously (the whole point of the
/// three-state gate is to render something - the landing page - for a visitor who isn't signed in
/// yet), so unlike <c>GuildPageBase</c> there is no <c>[Authorize]</c> upstream guaranteeing a
/// resolution always succeeds; a derived page's <see cref="OnPortalContextReadyAsync"/> override
/// typically branches on <see cref="Result"/>.Outcome the same way <c>PortalLayout</c> does.
/// </summary>
public abstract class PortalPageBase : ComponentBase, IDisposable
{
    /// <summary>
    /// The guild id from the route. <c>long</c>, not <c>ulong</c>, for the same routing-constraint
    /// reason as <c>GuildPageBase.GuildId</c> - Portal routes are declared <c>{guildId:long}</c>.
    /// </summary>
    [Parameter]
    public long GuildId { get; set; }

    [CascadingParameter]
    protected Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    [Inject]
    private IPortalContextProvider ContextProvider { get; set; } = default!;

    [Inject]
    private NavigationManager Nav { get; set; } = default!;

    [Inject]
    private PersistentComponentState ApplicationState { get; set; } = default!;

    /// <summary>The full result of resolving this route's guild id - null before the first resolution completes.</summary>
    protected PortalAccessResult? Result { get; private set; }

    /// <summary>Shorthand for <c>Result?.Context</c> - null only for <see cref="PortalAccessOutcome.GuildNotFound"/>.</summary>
    protected PortalContext? PortalGuild => Result?.Context;

    /// <summary>True while the initial (or a guild-id-changed) resolution is in flight.</summary>
    protected bool IsLoading { get; private set; } = true;

    private PersistingComponentStateSubscription _persistingSubscription;
    private ulong? _resolvedGuildId;

    private string PersistenceKey => $"PortalPageBase.PortalContext.{(ulong)GuildId}";

    protected sealed override async Task OnInitializedAsync()
    {
        _persistingSubscription = ApplicationState.RegisterOnPersisting(PersistCurrentResultAsync);

        if (ApplicationState.TryTakeFromJson<PortalAccessResult>(PersistenceKey, out var restored) && restored is not null)
        {
            Result = restored;
            IsLoading = false;
            _resolvedGuildId = (ulong)GuildId;
        }
        else
        {
            await ResolveAsync();
        }

        await OnPortalContextReadyAsync();
    }

    protected sealed override async Task OnParametersSetAsync()
    {
        if (_resolvedGuildId != (ulong)GuildId)
        {
            await ResolveAsync();
            await OnPortalContextReadyAsync();
        }
    }

    /// <summary>
    /// Called once after every (re)resolution of <see cref="Result"/> - override this instead of
    /// <c>OnInitializedAsync</c>/<c>OnParametersSetAsync</c>, both sealed here for the same
    /// can't-bypass-the-bookkeeping-by-accident reason as <c>GuildPageBase</c>.
    /// </summary>
    protected virtual Task OnPortalContextReadyAsync() => Task.CompletedTask;

    private async Task ResolveAsync()
    {
        IsLoading = true;
        var guildId = (ulong)GuildId;
        var user = AuthenticationStateTask is not null
            ? (await AuthenticationStateTask).User
            : new ClaimsPrincipal(new ClaimsIdentity());

        // Path only (no query string), matching PortalPageModelBase.CheckPortalAuthorizationAsync's
        // existing HttpContext.Request.Path usage.
        var returnPath = new Uri(Nav.Uri).AbsolutePath;

        Result = await ContextProvider.GetAsync(guildId, user, returnPath);
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
