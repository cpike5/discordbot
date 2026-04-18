using System.Reflection;
using System.Text.Json;
using DiscordBot.Bot.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering health check services.
/// </summary>
public static class HealthCheckExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Adds health check services including database and Discord gateway checks.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: new[] { "database" })
            .AddCheck<DiscordGatewayHealthCheck>("discord", tags: new[] { "discord" });

        return services;
    }

    /// <summary>
    /// Maps the health check endpoint at /health with anonymous access and JSON response.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static WebApplication MapDiscordBotHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
            },
            ResponseWriter = WriteHealthCheckResponse
        }).AllowAnonymous();

        return app;
    }

    /// <summary>
    /// Writes the health check response as JSON with per-check status breakdown.
    /// Exception details are suppressed from the response.
    /// </summary>
    private static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var checks = new Dictionary<string, object>();
        foreach (var entry in report.Entries)
        {
            checks[entry.Key] = new
            {
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description ?? string.Empty,
                duration = entry.Value.Duration.TotalMilliseconds
            };
        }

        var version = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow.ToString("o"),
            version,
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks
        };

        await context.Response.WriteAsJsonAsync(response, JsonOptions);
    }
}
