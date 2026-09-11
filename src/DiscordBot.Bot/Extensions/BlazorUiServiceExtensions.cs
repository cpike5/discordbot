using DiscordBot.Bot.Blazor.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering Blazor UI state services (toast queue, loading overlay
/// state). Not called from <c>Program.cs</c> yet — the Blazor hosting work wires this in once
/// <c>AddRazorComponents()</c> lands; see docs/plans/blazor-port-plan.md, Phase 1.
/// </summary>
public static class BlazorUiServiceExtensions
{
    /// <summary>
    /// Adds the Blazor UI state services: <see cref="IToastService"/> and
    /// <see cref="ILoadingState"/>, both scoped to a circuit so each connected user gets their own
    /// toast queue and loading flag.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlazorUiServices(this IServiceCollection services)
    {
        services.AddScoped<IToastService, ToastService>();
        services.AddScoped<ILoadingState, LoadingState>();

        return services;
    }
}
