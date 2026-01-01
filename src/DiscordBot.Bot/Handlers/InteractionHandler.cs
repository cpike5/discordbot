using System.Diagnostics;
using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Metrics;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
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
    private readonly ICommandExecutionLogger _commandExecutionLogger;
    private readonly IDashboardUpdateService _dashboardUpdateService;
    private readonly BotMetrics _botMetrics;

    // AsyncLocal storage for tracking execution context across async calls
    private static readonly AsyncLocal<ExecutionContext> _executionContext = new();

    public InteractionHandler(
        DiscordSocketClient client,
        InteractionService interactionService,
        IServiceProvider serviceProvider,
        IOptions<BotConfiguration> config,
        ILogger<InteractionHandler> logger,
        ICommandExecutionLogger commandExecutionLogger,
        IDashboardUpdateService dashboardUpdateService,
        BotMetrics botMetrics)
    {
        _client = client;
        _interactionService = interactionService;
        _serviceProvider = serviceProvider;
        _config = config.Value;
        _logger = logger;
        _commandExecutionLogger = commandExecutionLogger;
        _dashboardUpdateService = dashboardUpdateService;
        _botMetrics = botMetrics;
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
        _interactionService.ComponentCommandExecuted += OnComponentCommandExecutedAsync;

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
        catch (Discord.Net.HttpException ex) when (ex.DiscordCode == Discord.DiscordErrorCode.MissingPermissions)
        {
            _logger.LogWarning(
                "Missing access to register commands to guild {GuildId}. " +
                "Ensure the bot was invited with the 'applications.commands' scope. " +
                "Re-invite the bot using: https://discord.com/oauth2/authorize?client_id={ClientId}&scope=bot%20applications.commands&permissions=0",
                _config.TestGuildId,
                _client.CurrentUser.Id);

            // Fall back to global registration
            _logger.LogInformation("Falling back to global command registration");
            try
            {
                await _interactionService.RegisterCommandsGloballyAsync();
                _logger.LogInformation("Commands registered globally successfully. Note: Global commands may take up to 1 hour to propagate");
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Failed to register commands globally after guild registration failed");
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
        // Generate correlation ID for tracking this request
        var correlationId = Guid.NewGuid().ToString("N")[..16];
        var stopwatch = Stopwatch.StartNew();

        string? commandName = null;
        Activity? activity = null;

        // Extract command name and start tracing activity
        if (interaction is SocketSlashCommand slashCommand)
        {
            commandName = slashCommand.CommandName;
            _botMetrics.IncrementActiveCommands(commandName);

            // Start tracing activity for the command
            activity = BotActivitySource.StartCommandActivity(
                commandName: commandName,
                guildId: interaction.GuildId,
                userId: interaction.User.Id,
                interactionId: interaction.Id,
                correlationId: correlationId);
        }
        else if (interaction is SocketMessageComponent component)
        {
            // Start tracing activity for component interaction
            var componentType = component.Data.Type switch
            {
                ComponentType.Button => "button",
                ComponentType.SelectMenu => "select_menu",
                _ => "unknown"
            };

            activity = BotActivitySource.StartComponentActivity(
                componentType: componentType,
                customId: component.Data.CustomId,
                guildId: interaction.GuildId,
                userId: interaction.User.Id,
                interactionId: interaction.Id,
                correlationId: correlationId);
        }
        else if (interaction is SocketModal modal)
        {
            // Start tracing activity for modal submission
            activity = BotActivitySource.StartComponentActivity(
                componentType: "modal",
                customId: modal.Data.CustomId,
                guildId: interaction.GuildId,
                userId: interaction.User.Id,
                interactionId: interaction.Id,
                correlationId: correlationId);
        }

        // Store execution context for use in OnSlashCommandExecutedAsync
        _executionContext.Value = new ExecutionContext
        {
            CorrelationId = correlationId,
            Stopwatch = stopwatch,
            CommandName = commandName
        };

        try
        {
            // Create an execution context
            var context = new SocketInteractionContext(_client, interaction);

            // Use logging scope for correlation ID
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["InteractionId"] = interaction.Id,
                ["TraceId"] = activity?.TraceId.ToString() ?? "none"
            }))
            {
                _logger.LogDebug(
                    "Executing interaction {InteractionType} with correlation ID {CorrelationId}, TraceId {TraceId}",
                    interaction.Type,
                    correlationId,
                    activity?.TraceId.ToString() ?? "none");

                // Execute the command
                await _interactionService.ExecuteCommandAsync(context, _serviceProvider);
            }

            // Mark activity as successful if no exceptions
            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record exception on the tracing activity
            BotActivitySource.RecordException(activity, ex);

            // Record failure metric
            if (commandName != null)
            {
                _botMetrics.RecordCommandExecution(
                    commandName,
                    success: false,
                    durationMs: stopwatch.Elapsed.TotalMilliseconds,
                    guildId: interaction.GuildId);
            }

            _logger.LogError(
                ex,
                "Error executing interaction {InteractionId}, CorrelationId: {CorrelationId}, TraceId: {TraceId}",
                interaction.Id,
                correlationId,
                activity?.TraceId.ToString() ?? "none");

            // If the interaction hasn't been responded to, send an error message
            if (interaction.Type == InteractionType.ApplicationCommand)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("Error")
                    .WithDescription("An error occurred while executing this command.")
                    .WithColor(Color.Red)
                    .WithFooter($"Correlation ID: {correlationId}")
                    .WithCurrentTimestamp()
                    .Build();

                if (interaction.HasResponded)
                {
                    await interaction.FollowupAsync(embed: embed, ephemeral: true);
                }
                else
                {
                    await interaction.RespondAsync(embed: embed, ephemeral: true);
                }
            }
        }
        finally
        {
            // Dispose the activity (completes the span)
            activity?.Dispose();

            // Decrement active command count
            if (commandName != null)
            {
                _botMetrics.DecrementActiveCommands(commandName);
            }

            // Clear execution context
            _executionContext.Value = null!;
        }
    }

    /// <summary>
    /// Called after a slash command has been executed.
    /// Logs the result of the command execution and records it to the database.
    /// </summary>
    private async Task OnSlashCommandExecutedAsync(SlashCommandInfo commandInfo, IInteractionContext context, Discord.Interactions.IResult result)
    {
        // Get execution context
        var execContext = _executionContext.Value;
        var correlationId = execContext?.CorrelationId ?? "unknown";
        var stopwatch = execContext?.Stopwatch;

        stopwatch?.Stop();
        var executionTimeMs = (int)(stopwatch?.ElapsedMilliseconds ?? 0);

        var success = result.IsSuccess;
        var errorMessage = result.IsSuccess ? null : result.ErrorReason;

        // Build full command name including group prefix for subcommands
        var fullCommandName = GetFullCommandName(commandInfo);

        // Record command metrics
        _botMetrics.RecordCommandExecution(
            fullCommandName,
            success,
            executionTimeMs,
            context.Guild?.Id);

        // Log with correlation ID
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Slash command '{CommandName}' executed successfully by {Username} in guild {GuildName} (ID: {GuildId}), ExecutionTime: {ExecutionTimeMs}ms, CorrelationId: {CorrelationId}",
                    fullCommandName,
                    context.User.Username,
                    context.Guild?.Name ?? "DM",
                    context.Guild?.Id ?? 0,
                    executionTimeMs,
                    correlationId);
            }
            else
            {
                _logger.LogWarning(
                    "Slash command '{CommandName}' failed for {Username} in guild {GuildName} (ID: {GuildId}). Error: {Error}, ExecutionTime: {ExecutionTimeMs}ms, CorrelationId: {CorrelationId}",
                    fullCommandName,
                    context.User.Username,
                    context.Guild?.Name ?? "DM",
                    context.Guild?.Id ?? 0,
                    result.ErrorReason,
                    executionTimeMs,
                    correlationId);

                // Send enhanced error message to user for permission errors
                if (result.Error == InteractionCommandError.UnmetPrecondition)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("Permission Denied")
                        .WithDescription(errorMessage ?? "You do not have permission to use this command.")
                        .WithColor(Color.Red)
                        .WithFooter($"Correlation ID: {correlationId}")
                        .WithCurrentTimestamp()
                        .Build();

                    try
                    {
                        if (context.Interaction.HasResponded)
                        {
                            await context.Interaction.FollowupAsync(embed: embed, ephemeral: true);
                        }
                        else
                        {
                            await context.Interaction.RespondAsync(embed: embed, ephemeral: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send permission error message to user, CorrelationId: {CorrelationId}", correlationId);
                    }
                }
            }
        }

        // Log command execution to database (fire and forget with error handling inside)
        _ = _commandExecutionLogger.LogCommandExecutionAsync(
            context,
            fullCommandName,
            null, // Parameters - could be serialized if needed
            executionTimeMs,
            success,
            errorMessage,
            correlationId);

        // Broadcast command execution update to dashboard (fire-and-forget, failure tolerant)
        _ = BroadcastCommandExecutedAsync(fullCommandName, context, success);
    }

    /// <summary>
    /// Broadcasts command execution update to dashboard clients.
    /// Fire-and-forget with internal error handling.
    /// </summary>
    private async Task BroadcastCommandExecutedAsync(string commandName, IInteractionContext context, bool success)
    {
        try
        {
            var update = new CommandExecutedUpdateDto
            {
                CommandName = commandName,
                GuildId = context.Guild?.Id,
                GuildName = context.Guild?.Name,
                UserId = context.User.Id,
                Username = context.User.Username,
                Success = success,
                Timestamp = DateTime.UtcNow
            };

            await _dashboardUpdateService.BroadcastCommandExecutedAsync(update);
        }
        catch (Exception ex)
        {
            // Log but don't throw - this is fire-and-forget
            _logger.LogWarning(ex, "Failed to broadcast command executed update for {CommandName}, but continuing normal operation", commandName);
        }
    }

    /// <summary>
    /// Called after a component command (button, select menu, etc.) has been executed.
    /// Logs the result of the component interaction execution.
    /// </summary>
    private async Task OnComponentCommandExecutedAsync(ComponentCommandInfo commandInfo, IInteractionContext context, Discord.Interactions.IResult result)
    {
        // Get execution context
        var execContext = _executionContext.Value;
        var correlationId = execContext?.CorrelationId ?? "unknown";
        var stopwatch = execContext?.Stopwatch;

        stopwatch?.Stop();
        var executionTimeMs = (int)(stopwatch?.ElapsedMilliseconds ?? 0);

        // Record component metrics
        _botMetrics.RecordComponentInteraction(
            componentType: GetComponentType(context.Interaction),
            success: result.IsSuccess,
            durationMs: executionTimeMs);

        // Log with correlation ID
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "Component interaction '{CustomId}' executed successfully by {Username} in guild {GuildName} (ID: {GuildId}), ExecutionTime: {ExecutionTimeMs}ms, CorrelationId: {CorrelationId}",
                    commandInfo.Name,
                    context.User.Username,
                    context.Guild?.Name ?? "DM",
                    context.Guild?.Id ?? 0,
                    executionTimeMs,
                    correlationId);
            }
            else
            {
                _logger.LogWarning(
                    "Component interaction '{CustomId}' failed for {Username} in guild {GuildName} (ID: {GuildId}). Error: {Error}, ExecutionTime: {ExecutionTimeMs}ms, CorrelationId: {CorrelationId}",
                    commandInfo.Name,
                    context.User.Username,
                    context.Guild?.Name ?? "DM",
                    context.Guild?.Id ?? 0,
                    result.ErrorReason,
                    executionTimeMs,
                    correlationId);

                // Send error message to user for permission errors
                if (result.Error == InteractionCommandError.UnmetPrecondition)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("Permission Denied")
                        .WithDescription(result.ErrorReason ?? "You do not have permission to use this component.")
                        .WithColor(Color.Red)
                        .WithFooter($"Correlation ID: {correlationId}")
                        .WithCurrentTimestamp()
                        .Build();

                    try
                    {
                        if (context.Interaction.HasResponded)
                        {
                            await context.Interaction.FollowupAsync(embed: embed, ephemeral: true);
                        }
                        else
                        {
                            await context.Interaction.RespondAsync(embed: embed, ephemeral: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send permission error message to user, CorrelationId: {CorrelationId}", correlationId);
                    }
                }
            }
        }

        // We don't log component interactions to database since they're follow-up actions
        await Task.CompletedTask;
    }

    /// <summary>
    /// Determines the component type from a Discord interaction.
    /// </summary>
    private static string GetComponentType(IDiscordInteraction interaction)
    {
        return interaction switch
        {
            IComponentInteraction { Type: InteractionType.MessageComponent } comp
                => comp.Data.Type switch
                {
                    ComponentType.Button => "button",
                    ComponentType.SelectMenu => "select_menu",
                    _ => "unknown"
                },
            IModalInteraction => "modal",
            _ => "unknown"
        };
    }

    /// <summary>
    /// Gets the full command name including the group prefix for subcommands.
    /// For example, "consent status" instead of just "status".
    /// </summary>
    /// <param name="commandInfo">The slash command info.</param>
    /// <returns>The full command name with group prefix if applicable.</returns>
    private static string GetFullCommandName(SlashCommandInfo commandInfo)
    {
        return string.IsNullOrEmpty(commandInfo.Module.SlashGroupName)
            ? commandInfo.Name
            : $"{commandInfo.Module.SlashGroupName} {commandInfo.Name}";
    }

    /// <summary>
    /// Holds execution context for tracking command execution across async calls.
    /// </summary>
    private class ExecutionContext
    {
        public string CorrelationId { get; set; } = string.Empty;
        public Stopwatch? Stopwatch { get; set; }
        public string? CommandName { get; set; }
    }
}
