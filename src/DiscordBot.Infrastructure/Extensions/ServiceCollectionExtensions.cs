using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Configuration;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Interceptors;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        // Register DbContext with SQLite and interceptor
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=discordbot.db";

        services.AddDbContext<BotDbContext>((serviceProvider, options) =>
        {
            var interceptor = serviceProvider.GetRequiredService<QueryPerformanceInterceptor>();
            options.UseSqlite(connectionString)
                   .AddInterceptors(interceptor);
        });

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

        // Register services
        // SettingsService is registered as Singleton to maintain restart pending flag across requests
        services.AddSingleton<ISettingsService, SettingsService>();

        return services;
    }
}
