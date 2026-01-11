using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering analytics and metrics aggregation services.
/// </summary>
public static class AnalyticsServiceExtensions
{
    /// <summary>
    /// Adds analytics aggregation and retention services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAnalytics(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<AnalyticsRetentionOptions>(
            configuration.GetSection(AnalyticsRetentionOptions.SectionName));
        services.Configure<HistoricalMetricsOptions>(
            configuration.GetSection(HistoricalMetricsOptions.SectionName));

        // Analytics aggregation background services
        services.AddHostedService<MemberActivityAggregationService>();
        services.AddHostedService<ChannelActivityAggregationService>();
        services.AddHostedService<GuildMetricsAggregationService>();
        services.AddHostedService<AnalyticsRetentionService>();

        return services;
    }
}
