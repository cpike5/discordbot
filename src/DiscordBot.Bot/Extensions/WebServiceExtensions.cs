using DiscordBot.Bot.Blazor.Interop;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Bot.Handlers;
using Microsoft.AspNetCore.Components.Authorization;
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
        services.AddRazorPages();
        services.AddEndpointsApiExplorer();

        // Blazor Server foundation. Two hosting modes coexist during the migration
        // (docs/architecture/blazor-completion-plan.md Phase C):
        //  - islands embedded in Razor Pages via the component tag helper
        //    (blazor.server.js circuits), and
        //  - routed razor components served by MapRazorComponents<App> — pages
        //    migrate there one at a time, deleting their .cshtml as they go.
        // Both modes share the circuit hub mapped by AddInteractiveServerRenderMode.
        services.AddRazorComponents()
            .AddInteractiveServerComponents();
        services.AddServerSideBlazor();
        services.AddCascadingAuthenticationState();

        // Circuits outlive the auth cookie — revalidate connected users periodically.
        services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider>();

        // Per-circuit client info (IP/user agent) captured from the initial request
        // so audit logging keeps recording an address for in-circuit actions.
        services.AddScoped<CircuitClientInfoService>();

        // Interop bridges to the existing toast.js / theme.js modules so islands
        // reuse the portal's notification and theme systems. ChartJsInterop bridges
        // Chart.js (kept permanently) for chart-bearing Blazor pages.
        services.AddScoped<ToastInterop>();
        services.AddScoped<ThemeInterop>();
        services.AddScoped<ChartJsInterop>();

        // In-process event bus (Slice 2): real-time islands (NotificationBell,
        // BotStatusCard) subscribe to this instead of a second hub connection; the
        // existing notifier services dual-publish to it. Singleton so it can be
        // injected by the scoped NotificationBroadcaster and the singleton
        // DashboardUpdateService alike.
        services.AddSingleton<IDashboardEventBus, DashboardEventBus>();

        return services;
    }
}
