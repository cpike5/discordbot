using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Handlers;
using DiscordBot.Bot.Services;
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
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent | GatewayIntents.DirectMessages | GatewayIntents.GuildMembers,
                LogLevel = LogSeverity.Info,
                AlwaysDownloadUsers = false,
                MessageCacheSize = 100
            };

            return new DiscordSocketClient(config);
        });

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

        // Register InteractionStateService as singleton
        services.AddSingleton<IInteractionStateService, InteractionStateService>();

        // Register MessageLoggingHandler as singleton
        services.AddSingleton<MessageLoggingHandler>();

        // Register WelcomeHandler as singleton
        services.AddSingleton<WelcomeHandler>();

        // Register BotHostedService as hosted service
        services.AddHostedService<BotHostedService>();

        // Register InteractionStateCleanupService as hosted service
        services.AddHostedService<InteractionStateCleanupService>();

        return services;
    }
}
