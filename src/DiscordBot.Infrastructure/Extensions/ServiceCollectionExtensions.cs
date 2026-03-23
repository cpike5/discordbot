using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Configuration;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Interceptors;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Serilog;

namespace DiscordBot.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Infrastructure services including DbContext and repositories.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration settings
        services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName));

        // Register query performance interceptor as singleton
        services.AddSingleton<QueryPerformanceInterceptor>();

        // Register DbContext with provider detection
        // Primary: explicit Database:Provider config key; Fallback: connection string heuristic
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=data/discordbot.db";

        var dbSettings = configuration.GetSection(DatabaseSettings.SectionName).Get<DatabaseSettings>()
            ?? new DatabaseSettings();
        var isPostgreSql = dbSettings.IsPostgreSql(connectionString);

        var providerName = dbSettings.GetProviderDisplayName(connectionString);
        Log.Information("Database provider: {Provider}", providerName);

        if (isPostgreSql)
        {
            // Ensure TCP keepalive is set to detect dead connections early
            var csBuilder = new NpgsqlConnectionStringBuilder(connectionString);
            if (csBuilder.KeepAlive == 0)
                csBuilder.KeepAlive = 30;
            var npgsqlConnectionString = csBuilder.ToString();

            services.AddDbContext<PostgresBotDbContext>((serviceProvider, options) =>
            {
                var interceptor = serviceProvider.GetRequiredService<QueryPerformanceInterceptor>();
                options.UseNpgsql(npgsqlConnectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly("DiscordBot.Infrastructure");
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                })
                    .AddInterceptors(interceptor);
            });
            // Forward BotDbContext to resolve as PostgresBotDbContext
            services.AddScoped<BotDbContext>(sp => sp.GetRequiredService<PostgresBotDbContext>());
        }
        else
        {
            services.AddDbContext<BotDbContext>((serviceProvider, options) =>
            {
                var interceptor = serviceProvider.GetRequiredService<QueryPerformanceInterceptor>();
                options.UseSqlite(connectionString, sqlite =>
                    sqlite.MigrationsAssembly("DiscordBot.Infrastructure"))
                    .AddInterceptors(interceptor);
            });
        }

        // Register repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IGuildRepository, GuildRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICommandLogRepository, CommandLogRepository>();
        services.AddScoped<IMessageLogRepository, MessageLogRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IUserConsentRepository, UserConsentRepository>();
        services.AddScoped<IWelcomeConfigurationRepository, WelcomeConfigurationRepository>();
        services.AddScoped<IScheduledMessageRepository, ScheduledMessageRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IRatWatchRepository, RatWatchRepository>();
        services.AddScoped<IRatVoteRepository, RatVoteRepository>();
        services.AddScoped<IRatRecordRepository, RatRecordRepository>();
        services.AddScoped<IGuildRatWatchSettingsRepository, GuildRatWatchSettingsRepository>();
        services.AddScoped<IGuildMemberRepository, GuildMemberRepository>();
        services.AddScoped<IFlaggedEventRepository, FlaggedEventRepository>();
        services.AddScoped<IGuildModerationConfigRepository, GuildModerationConfigRepository>();
        services.AddScoped<IModerationCaseRepository, ModerationCaseRepository>();
        services.AddScoped<IModNoteRepository, ModNoteRepository>();
        services.AddScoped<IModTagRepository, ModTagRepository>();
        services.AddScoped<IUserModTagRepository, UserModTagRepository>();
        services.AddScoped<IWatchlistRepository, WatchlistRepository>();
        services.AddScoped<IReminderRepository, ReminderRepository>();
        services.AddScoped<IMemberActivityRepository, MemberActivityRepository>();
        services.AddScoped<IChannelActivityRepository, ChannelActivityRepository>();
        services.AddScoped<IGuildMetricsRepository, GuildMetricsRepository>();
        services.AddScoped<IPerformanceAlertRepository, PerformanceAlertRepository>();
        services.AddScoped<IMetricSnapshotRepository, MetricSnapshotRepository>();
        services.AddScoped<ISoundRepository, SoundRepository>();
        services.AddScoped<ISoundPlayLogRepository, SoundPlayLogRepository>();
        services.AddScoped<IGuildAudioSettingsRepository, GuildAudioSettingsRepository>();
        services.AddScoped<IUserSoundFavoriteRepository, UserSoundFavoriteRepository>();
        services.AddScoped<ITtsMessageRepository, TtsMessageRepository>();
        services.AddScoped<IGuildTtsSettingsRepository, GuildTtsSettingsRepository>();
        services.AddScoped<ICommandModuleConfigurationRepository, CommandModuleConfigurationRepository>();
        services.AddScoped<IUserActivityEventRepository, UserActivityEventRepository>();
        services.AddScoped<IThemeRepository, ThemeRepository>();
        services.AddScoped<IConnectionEventRepository, ConnectionEventRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IVoxMessageHistoryRepository, VoxMessageHistoryRepository>();
        services.AddScoped<IUserTtsPresetRepository, UserTtsPresetRepository>();
        services.AddScoped<IAudioPlaybackLogRepository, AudioPlaybackLogRepository>();

        // Register services
        // SettingsService is registered as Singleton to maintain restart pending flag across requests
        services.AddSingleton<ISettingsService, SettingsService>();
        // CommandModuleConfigurationService is registered as Singleton to maintain restart pending flag across requests
        services.AddSingleton<ICommandModuleConfigurationService, CommandModuleConfigurationService>();

        return services;
    }

}
