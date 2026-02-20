using DiscordBot.Bot.Components.Interop;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering Blazor JS interop services.
/// </summary>
public static class BlazorInteropServiceExtensions
{
    /// <summary>
    /// Adds Blazor JS interop services to the service collection.
    /// </summary>
    public static IServiceCollection AddBlazorInteropServices(this IServiceCollection services)
    {
        services.AddScoped<ToastInterop>();
        services.AddScoped<ThemeInterop>();
        services.AddScoped<ChartJsInterop>();
        services.AddScoped<TimezoneInterop>();
        services.AddScoped<ClipboardInterop>();
        services.AddScoped<NavigationInterop>();

        return services;
    }
}
