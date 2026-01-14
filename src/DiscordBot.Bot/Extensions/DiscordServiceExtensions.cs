using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Handlers;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Commands;
using DiscordBot.Bot.Services.Moderation;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering Discord bot services with dependency injection.
/// </summary>
public static class DiscordServiceExtensions
{
    /// <summary>
    /// Adds Discord bot services to the service collection.
    /// Registers DiscordSocketClient, InteractionService, InteractionHandler, and BotHostedService.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDiscordBot(this IServiceCollection services, IConfiguration configuration)
    {
        // Bind BotConfiguration from configuration
        services.Configure<BotConfiguration>(configuration.GetSection(BotConfiguration.SectionName));

        // Register DiscordSocketClient as singleton with configuration
        services.AddSingleton(provider =>
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent | GatewayIntents.DirectMessages | GatewayIntents.GuildMembers | GatewayIntents.GuildVoiceStates,
                LogLevel = LogSeverity.Info,
                AlwaysDownloadUsers = false,
                MessageCacheSize = 100
            };

            return new DiscordSocketClient(config);
        });

        // Register Discord client memory reporter for diagnostics
        services.AddSingleton<DiscordClientMemoryReporter>();
        services.AddSingleton<IMemoryReportable>(sp => sp.GetRequiredService<DiscordClientMemoryReporter>());

        // Register InteractionService as singleton
        services.AddSingleton(provider =>
        {
            var client = provider.GetRequiredService<DiscordSocketClient>();
            var config = new InteractionServiceConfig
            {
                LogLevel = LogSeverity.Info,
                DefaultRunMode = RunMode.Async
            };

            return new InteractionService(client, config);
        });

        // Register InteractionHandler as singleton
        services.AddSingleton<InteractionHandler>();

        // Register CommandExecutionLogger as singleton
        services.AddSingleton<ICommandExecutionLogger, CommandExecutionLogger>();

        // Register InteractionStateService as singleton (also implements IMemoryReportable)
        services.AddSingleton<InteractionStateService>();
        services.AddSingleton<IInteractionStateService>(sp => sp.GetRequiredService<InteractionStateService>());
        services.AddSingleton<IMemoryReportable>(sp => sp.GetRequiredService<InteractionStateService>());

        // Register MessageLoggingHandler as singleton
        services.AddSingleton<MessageLoggingHandler>();

        // Register ActivityEventTrackingHandler as singleton
        services.AddSingleton<ActivityEventTrackingHandler>();

        // Register WelcomeHandler as singleton
        services.AddSingleton<WelcomeHandler>();

        // Register MemberEventHandler as singleton
        services.AddSingleton<MemberEventHandler>();

        // Register VoiceStateHandler for real-time voice channel member count updates
        services.AddSingleton<VoiceStateHandler>();

        // Register member sync services
        services.AddSingleton<IMemberSyncQueue, MemberSyncQueue>();
        services.AddHostedService<MemberSyncService>();

        // Register BotHostedService as hosted service
        services.AddHostedService<BotHostedService>();

        // Register InteractionStateCleanupService as hosted service
        services.AddHostedService<InteractionStateCleanupService>();

        // Register analytics services
        services.AddScoped<IServerAnalyticsService, ServerAnalyticsService>();
        services.AddScoped<IModerationAnalyticsService, ModerationAnalyticsService>();
        services.AddScoped<IEngagementAnalyticsService, EngagementAnalyticsService>();

        return services;
    }
}
