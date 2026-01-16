using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Commands;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering core application services.
/// </summary>
public static class ApplicationServiceExtensions
{
    /// <summary>
    /// Adds core application services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Singleton services (application-wide state)
        services.AddSingleton<IVersionService, VersionService>();
        services.AddSingleton<IDashboardNotifier, DashboardNotifier>();
        services.AddSingleton<IAudioNotifier, AudioNotifier>();
        services.AddSingleton<IDashboardUpdateService, DashboardUpdateService>();
        services.AddSingleton<IPageMetadataService, PageMetadataService>();
        services.AddSingleton<IBotStatusService, BotStatusService>();

        // Scoped services (per-request)
        services.AddScoped<IBotService, BotService>();
        services.AddScoped<IGuildService, GuildService>();
        services.AddScoped<ICommandLogService, CommandLogService>();
        services.AddScoped<ICommandAnalyticsService, CommandAnalyticsService>();
        services.AddScoped<ICommandMetadataService, CommandMetadataService>();
        services.AddScoped<ICommandRegistrationService, CommandRegistrationService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IWelcomeService, WelcomeService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<ITimeParsingService, TimeParsingService>();
        services.AddScoped<IGuildMemberService, GuildMemberService>();
        services.AddScoped<IConsentService, ConsentService>();
        services.AddScoped<IUserPurgeService, UserPurgeService>();
        services.AddScoped<IUserDataExportService, UserDataExportService>();
        services.AddScoped<IBulkPurgeService, BulkPurgeService>();
        services.AddScoped<IThemeService, ThemeService>();

        // Metrics update background services
        services.AddHostedService<MetricsUpdateService>();
        services.AddHostedService<BusinessMetricsUpdateService>();

        return services;
    }
}
