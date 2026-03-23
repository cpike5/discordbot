using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Commands;
using DiscordBot.Bot.Services.Search;
using DiscordBot.Bot.Services.Tts;
using DiscordBot.Core.Configuration;
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
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind shared configuration options owned by core application services
        services.Configure<ApplicationOptions>(
            configuration.GetSection(ApplicationOptions.SectionName));
        services.Configure<CachingOptions>(
            configuration.GetSection(CachingOptions.SectionName));
        services.Configure<GuildMembershipCacheOptions>(
            configuration.GetSection(GuildMembershipCacheOptions.SectionName));
        services.Configure<BackgroundServicesOptions>(
            configuration.GetSection(BackgroundServicesOptions.SectionName));

        // Singleton services (application-wide state)
        services.AddSingleton<IBackgroundTaskRunner, BackgroundTaskRunner>();
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

        // Search providers — each handles one SearchCategory
        services.AddScoped<ISearchProvider, GuildsSearchProvider>();
        services.AddScoped<ISearchProvider, CommandLogsSearchProvider>();
        services.AddScoped<ISearchProvider, UsersSearchProvider>();
        services.AddScoped<ISearchProvider, CommandsSearchProvider>();
        services.AddScoped<ISearchProvider, AuditLogsSearchProvider>();
        services.AddScoped<ISearchProvider, MessageLogsSearchProvider>();
        services.AddScoped<ISearchProvider, PagesSearchProvider>();
        services.AddScoped<ISearchProvider, RemindersSearchProvider>();
        services.AddScoped<ISearchProvider, ScheduledMessagesSearchProvider>();
        services.AddScoped<ITimeParsingService, TimeParsingService>();
        services.AddScoped<IGuildMemberService, GuildMemberService>();
        services.AddScoped<IConsentService, ConsentService>();
        services.AddScoped<IUserPurgeService, UserPurgeService>();
        services.AddScoped<IUserDataExportService, UserDataExportService>();
        services.AddScoped<IBulkPurgeService, BulkPurgeService>();
        services.AddScoped<IThemeService, ThemeService>();
        services.AddScoped<ITtsPlaybackService, TtsPlaybackService>();
        services.AddScoped<ISoundboardOrchestrationService, SoundboardOrchestrationService>();

        // Metrics update background services
        services.AddHostedService<MetricsUpdateService>();
        services.AddHostedService<BusinessMetricsUpdateService>();

        return services;
    }
}
