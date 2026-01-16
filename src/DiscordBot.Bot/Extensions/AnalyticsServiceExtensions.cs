using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Moderation;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
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
    /// Registers IServerAnalyticsService, IModerationAnalyticsService, and IEngagementAnalyticsService
    /// along with background aggregation and retention services.
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

        // Register analytics services
        services.AddScoped<IServerAnalyticsService, ServerAnalyticsService>();
        services.AddScoped<IModerationAnalyticsService, ModerationAnalyticsService>();
        services.AddScoped<IEngagementAnalyticsService, EngagementAnalyticsService>();

        // Analytics aggregation background services
        services.AddHostedService<MemberActivityAggregationService>();
        services.AddHostedService<ChannelActivityAggregationService>();
        services.AddHostedService<GuildMetricsAggregationService>();
        services.AddHostedService<AnalyticsRetentionService>();

        return services;
    }
}
