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

        return services;
    }
}
