using DiscordBot.Bot.Blazor.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering the Blazor Web App hosting foundation: Razor Components
/// with Interactive Server render mode, the cascading auth state provider, and the Phase 1 auth
/// plumbing and circuit observability services under <c>Blazor/Services/</c>. See "Blazor
/// components" in <c>docs/architecture/patterns.md</c> and
/// <c>docs/plans/blazor-port-plan.md</c> §4.1/§4.2.
/// </summary>
public static class BlazorServiceExtensions
{
    /// <summary>
    /// Adds Blazor Web App hosting (Interactive Server only, per-page interactivity) alongside
    /// the existing Razor Pages/controllers pipeline.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="environment">
    /// The hosting environment, used to gate <c>DetailedErrors</c> to Development.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBlazorWeb(this IServiceCollection services, IWebHostEnvironment environment)
    {
        services.AddRazorComponents()
            .AddInteractiveServerComponents(options =>
            {
                options.DetailedErrors = environment.IsDevelopment();
                options.DisconnectedCircuitMaxRetained = 100;
                options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
            })
            .AddHubOptions(options =>
            {
                options.MaximumReceiveMessageSize = 64 * 1024;
            });

        // Makes Task<AuthenticationState> available as a cascading parameter to every
        // component, backed by the revalidating provider registered below.
        services.AddCascadingAuthenticationState();

        // Auth plumbing (plan §4.2): re-checks user existence/lockout/security-stamp every 30
        // minutes, since a circuit outlives the auth cookie that created it.
        services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider>();

        // Scoped per-circuit holder for IP/UA/correlation info, populated by the circuit
        // handler below, so components can audit-log without HttpContext (unavailable in a
        // circuit outside prerendering).
        services.AddScoped<CircuitClientInfoService>();

        // Circuit observability (plan §4.7 / Phase 1 deliverable 4): logs circuit open/close and
        // records the blazor.circuits.* metrics (registered with the other Metrics/ classes in
        // OpenTelemetryExtensions). Scoped -> one handler instance per circuit.
        services.AddScoped<CircuitHandler, BlazorCircuitHandler>();

        return services;
    }
}
