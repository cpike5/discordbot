using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering feature request services.
/// </summary>
public static class FeatureRequestServiceExtensions
{
    /// <summary>
    /// Adds feature request services including repository, and service.
    /// </summary>
    public static IServiceCollection AddFeatureRequests(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Repository (scoped — per-request lifetime with DbContext)
        services.AddScoped<IFeatureRequestRepository, FeatureRequestRepository>();

        // Service (scoped — depends on scoped repository)
        services.AddScoped<IFeatureRequestService, FeatureRequestService>();

        return services;
    }
}
