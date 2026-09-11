using DiscordBot.Bot.Services.Realtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering SignalR services.
/// </summary>
public static class SignalRServiceExtensions
{
    /// <summary>
    /// Adds SignalR services to the service collection for real-time dashboard updates.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="environment">The hosting environment.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSignalRServices(this IServiceCollection services, IWebHostEnvironment environment)
    {
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = environment.IsDevelopment();
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
            options.HandshakeTimeout = TimeSpan.FromSeconds(15);
        });

        // In-process event bus every dashboard broadcaster dual-publishes to alongside the hub
        // send, so Blazor components can subscribe without a SignalR client connection.
        // See docs/articles/signalr-realtime.md, "In-process event bus".
        services.AddSingleton<IDashboardEventBus, DashboardEventBus>();

        return services;
    }
}
