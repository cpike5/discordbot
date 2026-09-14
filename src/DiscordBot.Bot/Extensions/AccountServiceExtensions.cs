using DiscordBot.Bot.Services.Account;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Registers the account sign-in services behind the static SSR
/// <c>Blazor/Pages/Account/Login.razor</c> page and the <c>Account</c> minimal-API endpoints
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4c). Kept separate from
/// <c>IdentityServiceExtensions</c> - that file owns Identity/Discord OAuth/authorization
/// wiring itself, not the higher-level flows built on top of it.
/// </summary>
public static class AccountServiceExtensions
{
    /// <summary>Adds <see cref="IPasswordSignInService"/> and <see cref="IExternalLoginHandler"/>, scoped like the <c>SignInManager</c>/<c>UserManager</c> they wrap.</summary>
    public static IServiceCollection AddAccountServices(this IServiceCollection services)
    {
        services.AddScoped<IPasswordSignInService, PasswordSignInService>();
        services.AddScoped<IExternalLoginHandler, ExternalLoginHandler>();

        return services;
    }
}
