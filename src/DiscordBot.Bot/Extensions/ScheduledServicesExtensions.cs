using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering scheduled message and reminder services.
/// </summary>
public static class ScheduledServicesExtensions
{
    /// <summary>
    /// Adds scheduled message services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScheduledMessages(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<ScheduledMessagesOptions>(
            configuration.GetSection(ScheduledMessagesOptions.SectionName));

        // Scheduled message service (scoped for per-request)
        services.AddScoped<IScheduledMessageService, ScheduledMessageService>();

        // Background service for message execution
        services.AddHostedService<ScheduledMessageExecutionService>();

        return services;
    }

    /// <summary>
    /// Adds reminder services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddReminders(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<ReminderOptions>(
            configuration.GetSection(ReminderOptions.SectionName));

        // Reminder service (scoped for per-request)
        services.AddScoped<IReminderService, ReminderService>();

        // Background service for reminder execution
        services.AddHostedService<ReminderExecutionService>();

        return services;
    }
}
