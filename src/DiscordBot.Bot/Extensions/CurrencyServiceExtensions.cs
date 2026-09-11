using DiscordBot.Bot.Authorization;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Currency;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering the virtual currency services.
/// </summary>
public static class CurrencyServiceExtensions
{
    /// <summary>
    /// Adds the currency repositories, the wallet and currency services, and the charge and mint
    /// seams that priced features use.
    /// <para>
    /// Nothing here is registered when <c>Currency:Enabled</c> is false. That is the rollback
    /// path: with the services absent, the optional <c>IChargeService</c> a priced feature takes
    /// is null and every use of it is free.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCurrency(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<CurrencyOptions>(
            configuration.GetSection(CurrencyOptions.SectionName));

        // Repositories
        services.AddScoped<ICurrencyRepository, CurrencyRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<IPriceRepository, PriceRepository>();
        services.AddScoped<IMintAuthorityRepository, MintAuthorityRepository>();

        // Services
        services.AddScoped<ICurrencyService, CurrencyService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IChargeService, ChargeService>();
        services.AddScoped<IMintService, MintService>();

        // Who may act on a currency in the portal. Guild-keyed routes use the GuildAccess policy;
        // currency-keyed routes have no guild in the route, so they ask this instead.
        services.AddScoped<ICurrencyAccessService, CurrencyAccessService>();

        // Holds are in-process and must outlive a request, so the store is a singleton. Losing
        // them on restart means a free play, never a double charge.
        services.AddSingleton<IChargeHoldStore, ChargeHoldStore>();

        return services;
    }
}
