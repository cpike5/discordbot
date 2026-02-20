using DiscordBot.Bot.Components.Services;
using DiscordBot.Bot.Handlers;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
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
        // Add HttpClient for Discord API calls with tracing handler
        services.AddTransient<DiscordApiTracingHandler>();
        services.AddHttpClient("Discord", client =>
        {
            client.BaseAddress = new Uri("https://discord.com/api/v10/");
            client.DefaultRequestHeaders.Add("User-Agent", "DiscordBot-Admin");
        })
        .AddHttpMessageHandler<DiscordApiTracingHandler>();

        // Add Web API services
        services.AddControllers();
        services.AddRazorPages()
            .AddMvcOptions(options =>
            {
                options.Filters.Add<DiscordBot.Bot.Filters.DashboardAnonymousRedirectFilter>();
            });
        services.AddEndpointsApiExplorer();

        // Add Blazor Server components
        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        // Add cascading authentication state for Blazor component tree
        services.AddCascadingAuthenticationState();

        // Revalidate auth state periodically in Blazor Server circuits
        services.AddScoped<AuthenticationStateProvider, RevalidatingAuthStateProvider>();

        // Capture client IP during circuit initialization for audit logging
        services.AddScoped<CircuitClientInfoService>();
        services.AddScoped<CircuitHandler, CircuitClientInfoService>(sp => sp.GetRequiredService<CircuitClientInfoService>());

        return services;
    }
}
