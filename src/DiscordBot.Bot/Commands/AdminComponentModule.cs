using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Models;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Module for handling component interactions (button clicks) for admin commands.
/// </summary>
[RequireGuildActive]
public class AdminComponentModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IInteractionStateService _stateService;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ILogger<AdminComponentModule> _logger;

    public AdminComponentModule(
        IInteractionStateService stateService,
        IHostApplicationLifetime applicationLifetime,
        ILogger<AdminComponentModule> logger)
    {
        _stateService = stateService;
        _applicationLifetime = applicationLifetime;
        _logger = logger;
    }

    /// <summary>
    /// Handles the shutdown confirmation button interaction.
    /// </summary>
    [ComponentInteraction("shutdown:confirm:*:*:")]
    [Preconditions.RequireOwner]
    public async Task HandleShutdownConfirmAsync()
    {
        var customId = ((SocketMessageComponent)Context.Interaction).Data.CustomId;

        if (!ComponentIdBuilder.TryParse(customId, out var parts))
        {
            await RespondAsync("This interaction is invalid or has expired.", ephemeral: true);
            _logger.LogWarning("Invalid component ID format: {CustomId}", customId);
            return;
        }

        // Validate user
        if (parts.UserId != Context.User.Id)
        {
            await RespondAsync("You cannot interact with this component.", ephemeral: true);
            _logger.LogWarning(
                "User {ActualUserId} attempted to interact with component for user {ExpectedUserId}",
                Context.User.Id,
                parts.UserId);
            return;
        }

        // Retrieve state
        if (!_stateService.TryGetState<ShutdownState>(parts.CorrelationId, out var state) || state == null)
        {
            await RespondAsync("This interaction has expired. Please run the command again.", ephemeral: true);
            _logger.LogDebug("Expired or missing state for correlation ID {CorrelationId}", parts.CorrelationId);
            return;
        }

        // Remove state
        _stateService.TryRemoveState(parts.CorrelationId);

        // Update the original message to remove buttons
        var originalMessage = (SocketMessageComponent)Context.Interaction;
        await originalMessage.UpdateAsync(msg =>
        {
            msg.Content = "Shutdown confirmed. The bot is shutting down...";
            msg.Components = new ComponentBuilder().Build();
        });

        _logger.LogInformation(
            "Shutdown confirmed by user {UserId} ({Username}), initiating shutdown",
            Context.User.Id,
            Context.User.Username);

        // Initiate shutdown
        _applicationLifetime.StopApplication();
    }

    /// <summary>
    /// Handles the shutdown cancel button interaction.
    /// </summary>
    [ComponentInteraction("shutdown:cancel:*:*:")]
    public async Task HandleShutdownCancelAsync()
    {
        var customId = ((SocketMessageComponent)Context.Interaction).Data.CustomId;

        if (!ComponentIdBuilder.TryParse(customId, out var parts))
        {
            await RespondAsync("This interaction is invalid or has expired.", ephemeral: true);
            _logger.LogWarning("Invalid component ID format: {CustomId}", customId);
            return;
        }

        // Validate user
        if (parts.UserId != Context.User.Id)
        {
            await RespondAsync("You cannot interact with this component.", ephemeral: true);
            _logger.LogWarning(
                "User {ActualUserId} attempted to interact with component for user {ExpectedUserId}",
                Context.User.Id,
                parts.UserId);
            return;
        }

        // Retrieve state (optional, just to clean up)
        if (_stateService.TryGetState<ShutdownState>(parts.CorrelationId, out _))
        {
            _stateService.TryRemoveState(parts.CorrelationId);
        }

        // Update the original message to remove buttons
        var originalMessage = (SocketMessageComponent)Context.Interaction;
        await originalMessage.UpdateAsync(msg =>
        {
            msg.Content = "Shutdown cancelled.";
            msg.Components = new ComponentBuilder().Build();
        });

        _logger.LogInformation(
            "Shutdown cancelled by user {UserId} ({Username})",
            Context.User.Id,
            Context.User.Username);
    }

    /// <summary>
    /// Handles the guilds pagination button interaction.
    /// </summary>
    [ComponentInteraction("guilds:page:*:*:*")]
    public async Task HandleGuildsPageAsync()
    {
        var customId = ((SocketMessageComponent)Context.Interaction).Data.CustomId;

        if (!ComponentIdBuilder.TryParse(customId, out var parts))
        {
            await RespondAsync("This interaction is invalid or has expired.", ephemeral: true);
            _logger.LogWarning("Invalid component ID format: {CustomId}", customId);
            return;
        }

        // Validate user
        if (parts.UserId != Context.User.Id)
        {
            await RespondAsync("You cannot interact with this component.", ephemeral: true);
            _logger.LogWarning(
                "User {ActualUserId} attempted to interact with component for user {ExpectedUserId}",
                Context.User.Id,
                parts.UserId);
            return;
        }

        // Retrieve state
        if (!_stateService.TryGetState<GuildsPaginationState>(parts.CorrelationId, out var state) || state == null)
        {
            await RespondAsync("This interaction has expired. Please run the command again.", ephemeral: true);
            _logger.LogDebug("Expired or missing state for correlation ID {CorrelationId}", parts.CorrelationId);
            return;
        }

        // Parse page direction from data
        var direction = parts.Data;
        if (direction == "next" && state.CurrentPage < state.TotalPages - 1)
        {
            state.CurrentPage++;
        }
        else if (direction == "prev" && state.CurrentPage > 0)
        {
            state.CurrentPage--;
        }

        // Update state in service
        _stateService.TryRemoveState(parts.CorrelationId);
        var newCorrelationId = _stateService.CreateState(Context.User.Id, state);

        // Build the page content
        var pageGuilds = state.Guilds
            .Skip(state.CurrentPage * state.PageSize)
            .Take(state.PageSize)
            .ToList();

        var embed = new EmbedBuilder()
            .WithTitle($"Connected Guilds (Page {state.CurrentPage + 1}/{state.TotalPages})")
            .WithDescription(string.Join("\n", pageGuilds.Select(g => $"**{g.Name}** (ID: {g.Id})")))
            .WithColor(Color.Blue)
            .WithFooter($"Total: {state.Guilds.Count} guilds")
            .Build();

        // Build buttons
        var components = new ComponentBuilder();

        if (state.CurrentPage > 0)
        {
            var prevId = ComponentIdBuilder.Build("guilds", "page", Context.User.Id, newCorrelationId, "prev");
            components.WithButton("Previous", prevId, ButtonStyle.Primary);
        }

        if (state.CurrentPage < state.TotalPages - 1)
        {
            var nextId = ComponentIdBuilder.Build("guilds", "page", Context.User.Id, newCorrelationId, "next");
            components.WithButton("Next", nextId, ButtonStyle.Primary);
        }

        // Update the original message
        var originalMessage = (SocketMessageComponent)Context.Interaction;
        await originalMessage.UpdateAsync(msg =>
        {
            msg.Embed = embed;
            msg.Components = components.Build();
        });

        _logger.LogDebug(
            "Updated guilds pagination for user {UserId}, page {Page} of {TotalPages}",
            Context.User.Id,
            state.CurrentPage + 1,
            state.TotalPages);
    }
}
