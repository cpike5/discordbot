using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering Web API and Razor Pages services.
/// </summary>
public static class WebServiceExtensions
{
    /// <summary>
    /// Adds Web API and Razor Pages services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWebServices(this IServiceCollection services)
    {
        // Add HttpClient for Discord API calls with tracing handler.
        // The resilience handler retries transient failures (honoring Retry-After on 429) and
        // applies per-attempt/total timeouts so a hung Discord API call cannot block indefinitely.
        services.AddTransient<DiscordApiTracingHandler>();
        services.AddHttpClient("Discord", client =>
        {
            client.BaseAddress = new Uri("https://discord.com/api/v10/");
            client.DefaultRequestHeaders.Add("User-Agent", "DiscordBot-Admin");
        })
        .AddHttpMessageHandler<DiscordApiTracingHandler>()
        .AddBotResilienceHandler(attemptTimeout: TimeSpan.FromSeconds(10));

        // Add Web API services
        services.AddControllers();
        services.AddRazorPages();
        services.AddEndpointsApiExplorer();

        // Blazor Server foundation (islands-first modernization — see
        // docs/architecture/blazor-modernization-selective-plan.md). Interactive
        // components are embedded into existing Razor Pages via the component tag
        // helper; routing and auth stay with Razor Pages.
        services.AddServerSideBlazor();
        services.AddCascadingAuthenticationState();

        // Interop bridges to the existing toast.js / theme.js modules so islands
        // reuse the portal's notification and theme systems.
        services.AddScoped<ToastInterop>();
        services.AddScoped<ThemeInterop>();

        return services;
    }
}
