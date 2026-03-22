using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Notifications;
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
            configuration.GetSection(NotificationRetentionOptions.SectionName));

        services.Configure<NotificationOptions>(
            configuration.GetSection(NotificationOptions.SectionName));

        // Notification broadcaster (scoped — uses INotificationRepository which depends on DbContext)
        services.AddScoped<INotificationBroadcaster, NotificationBroadcaster>();

        // Notification service (scoped for per-request)
        services.AddScoped<INotificationService, NotificationService>();

        // Background retention cleanup service
        services.AddHostedService<NotificationRetentionService>();

        return services;
    }
}
