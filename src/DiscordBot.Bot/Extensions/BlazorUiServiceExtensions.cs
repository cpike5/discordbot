using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Portal;
using DiscordBot.Bot.Blazor.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering Blazor UI state services (toast queue, loading overlay
/// state, guild/portal context resolution). Called from <see cref="BlazorServiceExtensions.AddBlazorWeb"/>,
/// which <c>Program.cs</c> calls alongside <c>AddRazorComponents()</c>; see
/// docs/plans/blazor-port-plan.md, Phase 1 and Phase 3.
/// </summary>
public static class BlazorUiServiceExtensions
{
    /// <summary>
    /// Adds the Blazor UI state services: <see cref="IToastService"/> and
    /// <see cref="ILoadingState"/>, both scoped to a circuit so each connected user gets their own
    /// toast queue and loading flag; <see cref="IGuildContextProvider"/> (also scoped - it
    /// memoises per guild id for the lifetime of the scope, see "GuildContext" in
    /// <c>docs/architecture/patterns.md</c>); and <see cref="IPortalContextProvider"/> (same
    /// scoped-and-memoised shape, wrapping the existing <c>IPortalAccessService</c> - see
    /// "Portal three-state gate" in <c>docs/architecture/patterns.md</c>).
    /// <c>docs/architecture/patterns.md</c>); and <see cref="IUserGuildSelectorService"/>
    /// (<c>Search.razor</c>'s guild-intersection seam, plan §5 Phase 3).
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlazorUiServices(this IServiceCollection services)
    {
        services.AddScoped<IToastService, ToastService>();
        services.AddScoped<ILoadingState, LoadingState>();
        services.AddScoped<IGuildContextProvider, GuildContextProvider>();
        services.AddScoped<IPortalContextProvider, PortalContextProvider>();
        services.AddScoped<IUserGuildSelectorService, UserGuildSelectorService>();

        return services;
    }
}
