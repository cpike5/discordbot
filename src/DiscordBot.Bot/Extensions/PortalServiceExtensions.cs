using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Portal;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Registration for the member portal's view of Discord.
/// </summary>
public static class PortalServiceExtensions
{
    /// <summary>
    /// Registers <see cref="IPortalGuildDirectory"/>: the Discord-backed one, except when
    /// <see cref="DevelopmentPortal.IsEnabled"/> (Development environment <b>and</b>
    /// <c>Discord:OfflineMode</c>), which gets the database-backed development one.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="environment">The host environment.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPortalGuildDirectory(
        this IServiceCollection services,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        var offlineMode = configuration.GetValue<bool>($"{BotConfiguration.SectionName}:{nameof(BotConfiguration.OfflineMode)}");

        if (DevelopmentPortal.IsEnabled(environment, offlineMode))
        {
            services.AddScoped<IPortalGuildDirectory, DevelopmentPortalGuildDirectory>();
        }
        else
        {
            services.AddScoped<IPortalGuildDirectory, DiscordPortalGuildDirectory>();
        }

        return services;
    }
}
