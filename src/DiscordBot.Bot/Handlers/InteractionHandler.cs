using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Services;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Handlers;

/// <summary>
/// Handles Discord interaction events and command discovery/registration.
/// Discovers command modules from the assembly and registers them with Discord.
/// </summary>
public class InteractionHandler
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionService _interactionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly BotConfiguration _config;
    private readonly ILogger<InteractionHandler> _logger;

    public InteractionHandler(
        DiscordSocketClient client,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        IOptions<BotConfiguration> config,
        ILogger<InteractionHandler> logger)
    {
        _client = client;
        _interactionService = interactionService;
        _serviceProvider = serviceProvider;
        _config = config.Value;
        _logger = logger;
    }

    /// <summary>
    /// Initializes the interaction handler by discovering command modules and wiring up events.
    /// </summary>
    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing interaction handler");

        // Discover and add command modules from the executing assembly
        await _interactionService.AddModulesAsync(Assembly.GetExecutingAssembly(), _serviceProvider);

        // Wire up event handlers
        _client.Ready += OnReadyAsync;
        _client.InteractionCreated += OnInteractionCreatedAsync;
        _interactionService.SlashCommandExecuted += OnSlashCommandExecutedAsync;

        _logger.LogDebug("Interaction handler initialized with {ModuleCount} modules", _interactionService.Modules.Count());
    }

    /// <summary>
    /// Called when the bot is ready and connected to Discord.
    /// Registers slash commands either to a test guild or globally.
    /// </summary>
    private async Task OnReadyAsync()
    {
        _logger.LogInformation("Bot is ready. Connected as {Username}#{Discriminator}", _client.CurrentUser.Username, _client.CurrentUser.Discriminator);

        try
        {
            if (_config.TestGuildId.HasValue)
            {
                // Register commands to test guild for faster development iteration
                _logger.LogInformation("Registering commands to test guild {GuildId}", _config.TestGuildId.Value);
                await _interactionService.RegisterCommandsToGuildAsync(_config.TestGuildId.Value);
                _logger.LogInformation("Commands registered to test guild successfully");
            }
            else
            {
                // Register commands globally (takes ~1 hour to propagate)
                _logger.LogInformation("Registering commands globally");
                await _interactionService.RegisterCommandsGloballyAsync();
                _logger.LogInformation("Commands registered globally successfully. Note: Global commands may take up to 1 hour to propagate");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register commands");
        }
    }

    /// <summary>
    /// Called when an interaction is created (slash command, button click, etc.).
    /// Creates a context and executes the corresponding command.
    /// </summary>
    private async Task OnInteractionCreatedAsync(SocketInteraction interaction)
    {
        try
        {
            // Create an execution context
            var context = new SocketInteractionContext(_client, interaction);

            // Execute the command
            await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing interaction {InteractionId}", interaction.Id);

            // If the interaction hasn't been responded to, send an error message
            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                var followupText = "An error occurred while executing this command.";

                if (interaction.HasResponded)
                {
                    await interaction.FollowupAsync(followupText, ephemeral: true);
                }
                else
                {
                    await interaction.RespondAsync(followupText, ephemeral: true);
                }
            }
        }
    }

    /// <summary>
    /// Called after a slash command has been executed.
    /// Logs the result of the command execution.
    /// </summary>
    private Task OnSlashCommandExecutedAsync(SlashCommandInfo commandInfo, IInteractionContext context, Discord.Interactions.IResult result)
    {
        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "Slash command '{CommandName}' executed successfully by {Username} in guild {GuildName} (ID: {GuildId})",
                commandInfo.Name,
                context.User.Username,
                context.Guild?.Name ?? "DM",
                context.Guild?.Id ?? 0);
        }
        else
        {
            _logger.LogWarning(
                "Slash command '{CommandName}' failed for {Username} in guild {GuildName} (ID: {GuildId}). Error: {Error}",
                commandInfo.Name,
                context.User.Username,
                context.Guild?.Name ?? "DM",
                context.Guild?.Id ?? 0,
                result.ErrorReason);
        }

        return Task.CompletedTask;
    }
}
