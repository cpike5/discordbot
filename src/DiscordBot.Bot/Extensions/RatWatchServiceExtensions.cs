using DiscordBot.Bot.Services.RatWatch;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering Rat Watch services.
/// </summary>
public static class RatWatchServiceExtensions
{
    /// <summary>
    /// Adds Rat Watch services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRatWatch(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<RatWatchOptions>(
            configuration.GetSection(RatWatchOptions.SectionName));

        // Rat Watch services
        services.AddScoped<IRatWatchService, RatWatchService>();
        services.AddSingleton<IRatWatchStatusService, RatWatchStatusService>();

        // Background service for Rat Watch execution
        services.AddHostedService<RatWatchExecutionService>();

        return services;
    }
}
