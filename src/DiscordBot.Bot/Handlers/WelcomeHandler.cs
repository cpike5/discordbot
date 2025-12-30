using Discord.WebSocket;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Handles Discord UserJoined events to send welcome messages to new guild members.
/// Acts as a thin wrapper around the WelcomeService to bridge Discord.NET events.
/// </summary>
public class WelcomeHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WelcomeHandler> _logger;

    public WelcomeHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<WelcomeHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Handles the UserJoined event from DiscordSocketClient.
    /// Delegates to the WelcomeService to send welcome messages.
    /// </summary>
    /// <param name="user">The user who joined the guild.</param>
    public async Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        try
        {
            var guildId = user.Guild.Id;
            var userId = user.Id;

            _logger.LogDebug("Processing UserJoined event for user {UserId} ({Username}) in guild {GuildId} ({GuildName})",
                userId, user.Username, guildId, user.Guild.Name);

            // Create scope to access scoped services from singleton
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();

            // Check if welcome messages are globally enabled
            var isEnabled = await settingsService.GetSettingValueAsync<bool>("Features:WelcomeMessagesEnabled");
            if (!isEnabled)
            {
                _logger.LogDebug("Welcome messages are disabled globally, skipping for user {UserId} in guild {GuildId}",
                    userId, guildId);
                return;
            }

            var welcomeService = scope.ServiceProvider.GetRequiredService<IWelcomeService>();

            // Delegate to the welcome service
            var result = await welcomeService.SendWelcomeMessageAsync(guildId, userId);

            if (result)
            {
                _logger.LogInformation("Successfully sent welcome message for user {UserId} ({Username}) in guild {GuildId} ({GuildName})",
                    userId, user.Username, guildId, user.Guild.Name);
            }
            else
            {
                _logger.LogDebug("Welcome message was not sent for user {UserId} in guild {GuildId} (disabled or not configured)",
                    userId, guildId);
            }
        }
        catch (Exception ex)
        {
            // Log error but don't crash the bot
            _logger.LogError(ex,
                "Failed to handle UserJoined event for user {UserId} in guild {GuildId}",
                user.Id, user.Guild.Id);
        }
    }
}
