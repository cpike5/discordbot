using DiscordBot.Bot.Services;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering verification services.
/// </summary>
public static class VerificationServiceExtensions
{
    /// <summary>
    /// Adds verification services to the service collection.
    /// Note: VerificationOptions is configured separately in the main options registration block.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVerification(this IServiceCollection services)
    {
        // Verification service (scoped for per-request)
        services.AddScoped<IVerificationService, VerificationService>();

        // Background service for cleanup of expired verification codes
        services.AddHostedService<VerificationCleanupService>();

        return services;
    }
}
