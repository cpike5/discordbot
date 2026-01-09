using DiscordBot.Bot.Handlers;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Moderation;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering moderation services.
/// </summary>
public static class ModerationServiceExtensions
{
    /// <summary>
    /// Adds moderation system services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddModerationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind options
        services.Configure<ModerationOptions>(configuration.GetSection(ModerationOptions.SectionName));
        services.Configure<AutoModerationOptions>(configuration.GetSection(AutoModerationOptions.SectionName));

        // Core services (scoped)
        services.AddScoped<IModerationService, ModerationService>();
        services.AddScoped<IModNoteService, ModNoteService>();
        services.AddScoped<IModTagService, ModTagService>();
        services.AddScoped<IWatchlistService, WatchlistService>();
        services.AddScoped<IFlaggedEventService, FlaggedEventService>();
        services.AddScoped<IGuildModerationConfigService, GuildModerationConfigService>();
        services.AddScoped<IInvestigationService, InvestigationService>();

        // Detection services (singleton for in-memory caching)
        // Register concrete types first, then add interface mappings (including IMemoryReportable)
        services.AddSingleton<SpamDetectionService>();
        services.AddSingleton<ISpamDetectionService>(sp => sp.GetRequiredService<SpamDetectionService>());
        services.AddSingleton<IMemoryReportable>(sp => sp.GetRequiredService<SpamDetectionService>());

        services.AddSingleton<IContentFilterService, ContentFilterService>();

        services.AddSingleton<RaidDetectionService>();
        services.AddSingleton<IRaidDetectionService>(sp => sp.GetRequiredService<RaidDetectionService>());
        services.AddSingleton<IMemoryReportable>(sp => sp.GetRequiredService<RaidDetectionService>());

        // Handlers (singleton)
        services.AddSingleton<AutoModerationHandler>();

        return services;
    }
}
