using Microsoft.AspNetCore.Components.Authorization;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering the Blazor Web App hosting foundation: Razor Components
/// with Interactive Server render mode and cascading auth state. See "Blazor components" in
/// <c>docs/architecture/patterns.md</c> and <c>docs/plans/blazor-port-plan.md</c> §4.1.
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
        // component. The AuthenticationStateProvider backing it (and the circuit
        // observability services) are registered here too, added in the follow-up commit.
        services.AddCascadingAuthenticationState();

        return services;
    }
}
