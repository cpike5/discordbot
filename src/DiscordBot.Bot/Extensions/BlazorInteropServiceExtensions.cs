using DiscordBot.Bot.Blazor.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering the Blazor JS interop wrappers
/// (<see cref="ChartInterop"/>, <see cref="AudioInterop"/>, <see cref="BrowserInterop"/>,
/// <see cref="ThemeInterop"/>).
/// </summary>
public static class BlazorInteropServiceExtensions
{
    /// <summary>
    /// Registers the JS interop wrappers under <c>Blazor/Interop/</c> as scoped
    /// services — one instance per circuit, matching the lifetime of the
    /// <see cref="Microsoft.JSInterop.IJSRuntime"/> they wrap.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlazorInterop(this IServiceCollection services)
    {
        services.AddScoped<ChartInterop>();
        services.AddScoped<AudioInterop>();
        services.AddScoped<BrowserInterop>();
        services.AddScoped<ThemeInterop>();

        return services;
    }
}
