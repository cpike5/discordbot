using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Handlers;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Commands;
using DiscordBot.Bot.Services.DiscordIntegration;
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
        // Bind BotConfiguration from configuration with validation
        services.AddOptions<BotConfiguration>()
            .Bind(configuration.GetSection(BotConfiguration.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Register DiscordSocketClient as singleton with configuration
        services.AddSingleton(provider =>
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent | GatewayIntents.DirectMessages | GatewayIntents.GuildMembers | GatewayIntents.GuildVoiceStates,
                LogLevel = LogSeverity.Info,
                AlwaysDownloadUsers = false,
                MessageCacheSize = 100,
                EnableVoiceDaveEncryption = true
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

        // Register AssistantMessageHandler for AI assistant mentions
        services.AddSingleton<AssistantMessageHandler>();

        // Register DmAssistantMessageHandler for DM-based AI assistant
        services.AddSingleton<DmAssistantMessageHandler>();

        // Register FeatureRequestDmHandler — must be subscribed before DmAssistantMessageHandler
        services.AddSingleton<FeatureRequestDmHandler>();

        // Register Discord channel resolver as scoped
        services.AddScoped<IDiscordChannelResolver, DiscordChannelResolver>();

        // Register Discord user resolver for shared username/avatar lookups
        services.AddSingleton<IDiscordUserResolver, DiscordUserResolver>();

        // Register member sync services
        services.AddSingleton<IMemberSyncQueue, MemberSyncQueue>();

        // Register slash-command registration as its concrete type; the host starts/stops it
        // as a hosted service (see AddHostedService below). No other consumer exists, so it
        // is registered concretely rather than behind an interface.
        services.AddSingleton<SlashCommandRegistrationService>();

        // Register status/presence broadcasting behind its interface.
        services.AddSingleton<IBotUptimeProvider, BotUptimeProvider>();
        services.AddSingleton<IBotStatusBroadcaster, BotStatusBroadcaster>();

        // ==========================================================================
        // Hosted service startup order (Generic Host runs IHostedService.StartAsync
        // sequentially, in registration order — shutdown runs in reverse). Discord.Net
        // hosted services below MUST stay in this order:
        //
        //   1. MemberSyncService              — background queue processor; no gateway
        //                                        dependency, order-agnostic relative to login.
        //   2. SlashCommandRegistrationService — discovers/loads interaction modules and
        //                                        subscribes to Client.Ready BEFORE the
        //                                        gateway logs in, so modules are guaranteed
        //                                        loaded by the time Ready can fire.
        //   3. BotHostedService                — logs in and starts the gateway. Must run
        //                                        AFTER SlashCommandRegistrationService (2)
        //                                        for the reason above, and it is the service
        //                                        every other Discord-dependent hosted service
        //                                        (registered by other Add* extension methods
        //                                        in Program.cs) implicitly depends on being
        //                                        logged in first.
        //   4. InteractionStateCleanupService  — periodic cleanup; no ordering constraint,
        //                                        kept after login for consistency.
        //
        // See CLAUDE-REFERENCE.md ("Hosted Service Startup Order") for the full list across
        // all AddXxx extension methods called from Program.cs.
        // ==========================================================================
        services.AddHostedService<MemberSyncService>();
        services.AddHostedService(sp => sp.GetRequiredService<SlashCommandRegistrationService>());
        services.AddHostedService<BotHostedService>();
        services.AddHostedService<InteractionStateCleanupService>();

        return services;
    }
}
