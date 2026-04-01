using DiscordBot.Bot.Services.FeatureRequests;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using DiscordBot.Infrastructure.Services.FeatureRequests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering feature request services.
/// </summary>
public static class FeatureRequestServiceExtensions
{
    /// <summary>
    /// Adds feature request services including repository, service, queue, and configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFeatureRequests(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind options
        services.Configure<FeatureRequestsOptions>(
            configuration.GetSection(FeatureRequestsOptions.SectionName));

        // Repository (scoped — per-request lifetime with DbContext)
        services.AddScoped<IFeatureRequestRepository, FeatureRequestRepository>();

        // Service (scoped — depends on scoped repository)
        services.AddScoped<IFeatureRequestService, FeatureRequestService>();

        // Singleton queue — shared across all requests and background workers
        services.AddSingleton<IFeatureRequestDocGenQueue, FeatureRequestDocGenQueue>();

        // Singleton process runner — stateless, safe to share
        services.AddSingleton<IClaudeCodeProcessRunner, ClaudeCodeProcessRunner>();

        // Singleton slugifier — stateless utility
        services.AddSingleton<FeatureNameSlugifier>();

        // Validation services — both are singleton-safe (stateless, depend only on singletons)
        services.AddSingleton<PromptInjectionFilter>();
        services.AddSingleton<IInputValidationService, InputValidationService>();

        // Singleton conversation service (holds session dictionary)
        services.AddSingleton<FeatureRequestConversationService>();

        // Background doc generation hosted service
        services.AddHostedService<FeatureRequestDocGenService>();

        return services;
    }
}
