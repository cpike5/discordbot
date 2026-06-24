using DiscordBot.Bot.Handlers;
using DiscordBot.Bot.Services.NotX;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering not-X feature services.
/// </summary>
public static class NotXServiceExtensions
{
    /// <summary>
    /// Adds all services required by the not-X X/Twitter link preview feature.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNotX(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind operator-level tuning options
        services.Configure<NotXOptions>(
            configuration.GetSection(NotXOptions.SectionName));

        // Named HTTP client — single-purpose, points only at api.fxtwitter.com.
        // The configured request timeout is applied per attempt by the resilience handler, which
        // also retries transient failures with jittered backoff and trips a circuit breaker when
        // the upstream is consistently failing. The pipeline (not HttpClient.Timeout) owns timing
        // so retries are not cut short by a global client timeout.
        var fxTwitterTimeout = TimeSpan.FromSeconds(
            configuration.GetValue("NotX:RequestTimeoutSeconds", 5));
        services.AddHttpClient("FxTwitter", client =>
        {
            client.BaseAddress = new Uri("https://api.fxtwitter.com/");
            client.DefaultRequestHeaders.UserAgent
                .ParseAdd(configuration.GetValue("NotX:UserAgent", "discordbot/1.0"));
        })
        .AddBotResilienceHandler(attemptTimeout: fxTwitterTimeout);

        // Scoped services
        services.AddScoped<IFxTwitterClient, FxTwitterClient>();
        services.AddScoped<INotXService, NotXService>();

        // Handler is singleton (same lifetime as DiscordSocketClient)
        services.AddSingleton<NotXMessageHandler>();

        return services;
    }
}
