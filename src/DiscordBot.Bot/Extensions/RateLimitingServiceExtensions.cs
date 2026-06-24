using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering request rate limiting policies.
/// </summary>
public static class RateLimitingServiceExtensions
{
    /// <summary>
    /// Rate limit policy name for anonymous, CPU-bound SSML endpoints
    /// (validation/building). These endpoints accept unauthenticated input and run
    /// parsing/validation work, making them a denial-of-service vector without a limit.
    /// </summary>
    public const string AnonymousSsmlPolicy = "ssml-anonymous";

    /// <summary>
    /// Adds rate limiting services and the portal's per-policy limiters.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPortalRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Per-IP fixed window for anonymous SSML endpoints. RemoteIpAddress is the
            // real client IP because UseForwardedHeaders runs first in the pipeline.
            options.AddPolicy(AnonymousSsmlPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));
        });

        return services;
    }
}
