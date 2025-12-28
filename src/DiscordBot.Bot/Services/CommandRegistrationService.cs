using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing Discord slash command registration.
/// Provides functionality to clear and re-register commands globally.
/// </summary>
public class CommandRegistrationService : ICommandRegistrationService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;
    private readonly ILogger<CommandRegistrationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandRegistrationService"/> class.
    /// </summary>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="interactionService">The interaction service.</param>
    /// <param name="logger">The logger.</param>
    public CommandRegistrationService(
        DiscordSocketClient client,
        InteractionService interactionService,
        ILogger<CommandRegistrationService> logger)
    {
        _client = client;
        _interactionService = interactionService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CommandRegistrationResult> ClearAndRegisterGloballyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting command registration process: clearing all commands and re-registering globally");

        try
        {
            // Ensure bot is connected
            if (_client.ConnectionState != ConnectionState.Connected)
            {
                _logger.LogWarning("Cannot register commands: Discord client is not connected (state: {ConnectionState})",
                    _client.ConnectionState);
                return new CommandRegistrationResult
                {
                    Success = false,
                    Message = $"Bot is not connected to Discord (state: {_client.ConnectionState}). Please wait for the bot to connect and try again."
                };
            }

            var guildsCleared = 0;

            // Step 1: Clear global commands by re-registering with empty array
            _logger.LogDebug("Clearing global commands");
            try
            {
                await _client.BulkOverwriteGlobalApplicationCommandsAsync(Array.Empty<ApplicationCommandProperties>());
                _logger.LogInformation("Global commands cleared successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clear global commands");
                throw;
            }

            // Step 2: Clear guild-specific commands for all guilds
            _logger.LogDebug("Clearing guild-specific commands for {GuildCount} guilds", _client.Guilds.Count);

            foreach (var guild in _client.Guilds)
            {
                try
                {
                    await guild.BulkOverwriteApplicationCommandAsync(Array.Empty<ApplicationCommandProperties>());
                    guildsCleared++;
                    _logger.LogDebug("Cleared commands for guild {GuildId} ({GuildName})", guild.Id, guild.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clear commands for guild {GuildId} ({GuildName})", guild.Id, guild.Name);
                    // Continue with other guilds even if one fails
                }
            }

            _logger.LogInformation("Cleared commands from {GuildsCleared} guild(s)", guildsCleared);

            // Step 3: Re-register commands globally
            _logger.LogDebug("Re-registering commands globally");
            await _interactionService.RegisterCommandsGloballyAsync();

            var commandCount = _interactionService.SlashCommands.Count;
            _logger.LogInformation("Successfully re-registered {CommandCount} command(s) globally", commandCount);

            return new CommandRegistrationResult
            {
                Success = true,
                Message = $"Successfully cleared and re-registered {commandCount} command(s) globally. Commands cleared from {guildsCleared} guild(s). Global commands may take up to 1 hour to propagate.",
                GlobalCommandsRegistered = commandCount,
                GuildsCleared = guildsCleared
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear and re-register commands");
            return new CommandRegistrationResult
            {
                Success = false,
                Message = $"Failed to register commands: {ex.Message}"
            };
        }
    }
}
