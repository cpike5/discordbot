using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Models;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Module for handling component interactions (button clicks, select menus) for scheduled message commands.
/// </summary>
public class ScheduleComponentModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly IInteractionStateService _stateService;
    private readonly ILogger<ScheduleComponentModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleComponentModule"/> class.
    /// </summary>
    public ScheduleComponentModule(
        IScheduledMessageService scheduledMessageService,
        IInteractionStateService stateService,
        ILogger<ScheduleComponentModule> logger)
    {
        _scheduledMessageService = scheduledMessageService;
        _stateService = stateService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the delete confirmation button interaction.
    /// </summary>
    [ComponentInteraction("schedule:delete-confirm:*:*:")]
    public async Task HandleDeleteConfirmAsync()
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
        if (!_stateService.TryGetState<ScheduleDeleteState>(parts.CorrelationId, out var state) || state == null)
        {
            await RespondAsync("This interaction has expired. Please run the command again.", ephemeral: true);
            _logger.LogDebug("Expired or missing state for correlation ID {CorrelationId}", parts.CorrelationId);
            return;
        }

        // Remove state
        _stateService.TryRemoveState(parts.CorrelationId);

        try
        {
            var success = await _scheduledMessageService.DeleteAsync(state.MessageId);

            if (success)
            {
                _logger.LogInformation(
                    "Scheduled message {MessageId} deleted by user {UserId} ({Username})",
                    state.MessageId,
                    Context.User.Id,
                    Context.User.Username);

                // Update the original message to remove buttons
                var originalMessage = (SocketMessageComponent)Context.Interaction;
                await originalMessage.UpdateAsync(msg =>
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("✅ Message Deleted")
                        .WithDescription($"The scheduled message **{state.MessageTitle}** has been deleted successfully.")
                        .WithColor(Color.Green)
                        .WithCurrentTimestamp()
                        .Build();

                    msg.Embed = embed;
                    msg.Components = new ComponentBuilder().Build();
                });
            }
            else
            {
                _logger.LogWarning("Failed to delete scheduled message {MessageId} - not found", state.MessageId);

                var originalMessage = (SocketMessageComponent)Context.Interaction;
                await originalMessage.UpdateAsync(msg =>
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("❌ Error")
                        .WithDescription("Failed to delete the scheduled message. It may have already been deleted.")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp()
                        .Build();

                    msg.Embed = embed;
                    msg.Components = new ComponentBuilder().Build();
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while deleting scheduled message {MessageId}", state.MessageId);

            var originalMessage = (SocketMessageComponent)Context.Interaction;
            await originalMessage.UpdateAsync(msg =>
            {
                var embed = new EmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription($"An error occurred while deleting the scheduled message: {ex.Message}")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                msg.Embed = embed;
                msg.Components = new ComponentBuilder().Build();
            });
        }
    }

    /// <summary>
    /// Handles the delete cancel button interaction.
    /// </summary>
    [ComponentInteraction("schedule:delete-cancel:*:*:")]
    public async Task HandleDeleteCancelAsync()
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
        if (_stateService.TryGetState<ScheduleDeleteState>(parts.CorrelationId, out _))
        {
            _stateService.TryRemoveState(parts.CorrelationId);
        }

        // Update the original message to remove buttons
        var originalMessage = (SocketMessageComponent)Context.Interaction;
        await originalMessage.UpdateAsync(msg =>
        {
            var embed = new EmbedBuilder()
                .WithTitle("🔙 Delete Cancelled")
                .WithDescription("The scheduled message was not deleted.")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp()
                .Build();

            msg.Embed = embed;
            msg.Components = new ComponentBuilder().Build();
        });

        _logger.LogInformation(
            "Scheduled message delete cancelled by user {UserId} ({Username})",
            Context.User.Id,
            Context.User.Username);
    }

    /// <summary>
    /// Handles the list select menu interaction to show message details.
    /// </summary>
    [ComponentInteraction("schedule:list-select:*:*:")]
    public async Task HandleListSelectAsync()
    {
        var customId = ((SocketMessageComponent)Context.Interaction).Data.CustomId;
        var selectedValue = ((SocketMessageComponent)Context.Interaction).Data.Values.FirstOrDefault();

        if (string.IsNullOrEmpty(selectedValue) || !Guid.TryParse(selectedValue, out var messageId))
        {
            await RespondAsync("Invalid selection.", ephemeral: true);
            _logger.LogWarning("Invalid selection value: {SelectedValue}", selectedValue);
            return;
        }

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
        if (!_stateService.TryGetState<ScheduleListState>(parts.CorrelationId, out var state) || state == null)
        {
            await RespondAsync("This interaction has expired. Please run the command again.", ephemeral: true);
            _logger.LogDebug("Expired or missing state for correlation ID {CorrelationId}", parts.CorrelationId);
            return;
        }

        try
        {
            // Get the full message details
            var message = await _scheduledMessageService.GetByIdAsync(messageId);

            if (message == null)
            {
                await RespondAsync("Scheduled message not found.", ephemeral: true);
                _logger.LogWarning("Scheduled message {MessageId} not found", messageId);
                return;
            }

            _logger.LogDebug(
                "Displaying details for scheduled message {MessageId} to user {UserId}",
                messageId,
                Context.User.Id);

            var statusIcon = message.IsEnabled ? "✅ Enabled" : "❌ Disabled";
            var frequencyText = message.Frequency == ScheduleFrequency.Custom
                ? $"Custom ({message.CronExpression})"
                : message.Frequency.ToString();

            var embed = new EmbedBuilder()
                .WithTitle($"📅 {message.Title}")
                .WithColor(message.IsEnabled ? Color.Green : Color.Orange)
                .AddField("Status", statusIcon, inline: true)
                .AddField("Frequency", frequencyText, inline: true)
                .AddField("Channel", $"<#{message.ChannelId}>", inline: true)
                .AddField("Message ID", message.Id.ToString(), inline: false)
                .AddField("Message Content", message.Content.Length > 1024 ? message.Content.Substring(0, 1021) + "..." : message.Content, inline: false);

            if (message.NextExecutionAt.HasValue)
            {
                embed.AddField(
                    "Next Execution",
                    $"<t:{new DateTimeOffset(message.NextExecutionAt.Value).ToUnixTimeSeconds()}:F> (<t:{new DateTimeOffset(message.NextExecutionAt.Value).ToUnixTimeSeconds()}:R>)",
                    inline: false);
            }

            if (message.LastExecutedAt.HasValue)
            {
                embed.AddField(
                    "Last Executed",
                    $"<t:{new DateTimeOffset(message.LastExecutedAt.Value).ToUnixTimeSeconds()}:F> (<t:{new DateTimeOffset(message.LastExecutedAt.Value).ToUnixTimeSeconds()}:R>)",
                    inline: false);
            }

            embed.AddField("Created", $"<t:{new DateTimeOffset(message.CreatedAt).ToUnixTimeSeconds()}:F>", inline: true);
            embed.AddField("Created By", $"<@{message.CreatedBy}>", inline: true);
            embed.WithCurrentTimestamp();
            embed.WithFooter("Admin Command");

            await RespondAsync(embed: embed.Build(), ephemeral: true);

            _logger.LogDebug("Message details sent successfully for message {MessageId}", messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while displaying scheduled message details for {MessageId}", messageId);

            await RespondAsync($"An error occurred while retrieving message details: {ex.Message}", ephemeral: true);
        }
    }
}
