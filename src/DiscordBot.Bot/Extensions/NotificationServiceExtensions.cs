using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering notification services.
/// </summary>
public static class NotificationServiceExtensions
{
    /// <summary>
    /// Adds notification services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNotificationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<NotificationRetentionOptions>(
            configuration.GetSection("NotificationRetention"));

        services.Configure<NotificationOptions>(
            configuration.GetSection(NotificationOptions.SectionName));

        // Notification service (scoped for per-request)
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
