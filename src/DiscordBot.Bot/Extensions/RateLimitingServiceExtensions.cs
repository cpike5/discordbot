using System.Security.Claims;
using System.Threading.RateLimiting;
using DiscordBot.Core.Configuration;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Names of the rate-limit policies, for <c>[EnableRateLimiting(...)]</c>.
/// </summary>
public static class PortalRateLimitPolicies
{
    /// <summary>Portal soundboard uploads, per user.</summary>
    public const string Upload = "portal-upload";

    /// <summary>Portal soundboard plays, per user.</summary>
    public const string Play = "portal-play";
}

/// <summary>
/// Registers ASP.NET Core rate limiting for the member portal endpoints.
/// Requires <c>app.UseRateLimiter()</c> after authentication, so the partition key sees the user.
/// </summary>
public static class RateLimitingServiceExtensions
{
    /// <summary>
    /// Adds the <see cref="PortalRateLimitPolicies"/> policies: a fixed window per signed-in user,
    /// sized by <see cref="PortalRateLimitOptions"/>. A refused request gets 429 with a problem JSON body.
    /// </summary>
    public static IServiceCollection AddPortalRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PortalRateLimitOptions>(configuration.GetSection(PortalRateLimitOptions.SectionName));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                await context.HttpContext.Response.WriteProblemAsync(
                    StatusCodes.Status429TooManyRequests,
                    "Too many requests",
                    "You're doing that too often. Wait a moment and try again.");
            };

            options.AddPolicy(PortalRateLimitPolicies.Upload, context =>
                FixedWindowPerUser(context, o => o.Upload));
            options.AddPolicy(PortalRateLimitPolicies.Play, context =>
                FixedWindowPerUser(context, o => o.Play));
        });

        return services;
    }

    /// <summary>
    /// The key one caller's requests are counted under: the Discord user id when the account is
    /// linked, otherwise the Identity user id, otherwise the client address.
    /// </summary>
    internal static string GetPartitionKey(HttpContext context)
    {
        var discordUserId = context.User.FindFirst(ClaimsPrincipalExtensions.DiscordUserIdClaimType)?.Value;
        if (!string.IsNullOrEmpty(discordUserId))
        {
            return $"discord:{discordUserId}";
        }

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            return $"user:{userId}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }

    private static RateLimitPartition<string> FixedWindowPerUser(
        HttpContext context,
        Func<PortalRateLimitOptions, FixedWindowLimit> select)
    {
        var limit = select(context.RequestServices.GetRequiredService<IOptions<PortalRateLimitOptions>>().Value);

        return RateLimitPartition.GetFixedWindowLimiter(GetPartitionKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, limit.PermitLimit),
            Window = TimeSpan.FromSeconds(Math.Max(1, limit.WindowSeconds)),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }
}
