using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Models;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash command module for managing scheduled messages in Discord guilds.
/// </summary>
[RequireAdmin]
public class ScheduleModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly IInteractionStateService _stateService;
    private readonly DiscordSocketClient _client;
    private readonly ILogger<ScheduleModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleModule"/> class.
    /// </summary>
    public ScheduleModule(
        IScheduledMessageService scheduledMessageService,
        IInteractionStateService stateService,
        DiscordSocketClient client,
        ILogger<ScheduleModule> logger)
    {
        _scheduledMessageService = scheduledMessageService;
        _stateService = stateService;
        _client = client;
        _logger = logger;
    }

    /// <summary>
    /// Lists all scheduled messages for the current guild with pagination.
    /// </summary>
    [SlashCommand("schedule-list", "Show scheduled messages for this guild")]
    public async Task ListAsync()
    {
        _logger.LogInformation(
            "Schedule list command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        var guildId = Context.Guild.Id;
        const int pageSize = 10;

        var (items, totalCount) = await _scheduledMessageService.GetByGuildIdAsync(guildId, 1, pageSize);

        _logger.LogDebug(
            "Retrieved {Count} of {TotalCount} scheduled messages for guild {GuildId}",
            items.Count(),
            totalCount,
            guildId);

        if (totalCount == 0)
        {
            var emptyEmbed = new EmbedBuilder()
                .WithTitle("📅 Scheduled Messages")
                .WithDescription("No scheduled messages found for this guild.")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: emptyEmbed, ephemeral: true);
            _logger.LogDebug("Schedule list command response sent (no messages)");
            return;
        }

        var messagesList = items.ToList();

        // Build the embed
        var embed = new EmbedBuilder()
            .WithTitle($"📅 Scheduled Messages (Page 1/{Math.Ceiling(totalCount / (double)pageSize)})")
            .WithColor(Color.Blue)
            .WithFooter($"Total: {totalCount} scheduled messages")
            .WithCurrentTimestamp();

        foreach (var msg in messagesList)
        {
            var statusIcon = msg.IsEnabled ? "✅" : "❌";
            var frequencyText = msg.Frequency == ScheduleFrequency.Custom
                ? $"Custom ({msg.CronExpression})"
                : msg.Frequency.ToString();

            var nextExecution = msg.NextExecutionAt.HasValue
                ? $"<t:{new DateTimeOffset(msg.NextExecutionAt.Value).ToUnixTimeSeconds()}:R>"
                : "Not scheduled";

            embed.AddField(
                $"{statusIcon} {msg.Title}",
                $"**Frequency:** {frequencyText}\n**Next:** {nextExecution}\n**ID:** `{msg.Id}`",
                inline: false);
        }

        // Build select menu for viewing details
        var selectMenuBuilder = new SelectMenuBuilder()
            .WithCustomId("schedule-list-select")
            .WithPlaceholder("Select a message to view details");

        foreach (var msg in messagesList.Take(25)) // Discord limit of 25 options
        {
            var label = msg.Title.Length > 100 ? msg.Title.Substring(0, 97) + "..." : msg.Title;
            selectMenuBuilder.AddOption(label, msg.Id.ToString(), $"{msg.Frequency} - {(msg.IsEnabled ? "Enabled" : "Disabled")}");
        }

        var state = new ScheduleListState
        {
            Messages = messagesList,
            GuildId = guildId
        };

        var correlationId = _stateService.CreateState(Context.User.Id, state);
        var customId = ComponentIdBuilder.Build("schedule", "list-select", Context.User.Id, correlationId);
        selectMenuBuilder.WithCustomId(customId);

        var components = new ComponentBuilder()
            .WithSelectMenu(selectMenuBuilder)
            .Build();

        await RespondAsync(embed: embed.Build(), components: components, ephemeral: true);

        _logger.LogDebug("Schedule list command response sent successfully");
    }

    /// <summary>
    /// Creates a new scheduled message.
    /// </summary>
    [SlashCommand("schedule-create", "Create a new scheduled message")]
    public async Task CreateAsync(
        [Summary("title", "Title of the scheduled message")] string title,
        [Summary("channel", "Channel where the message will be sent")] ITextChannel channel,
        [Summary("message", "Content of the message to send")] string message,
        [Summary("frequency", "How often the message should be sent")] ScheduleFrequency frequency,
        [Summary("cron", "Cron expression (required for Custom frequency)")] string? cron = null)
    {
        _logger.LogInformation(
            "Schedule create command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), Title: {Title}, Frequency: {Frequency}",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            Context.Guild.Id,
            title,
            frequency);

        // Validate cron expression for Custom frequency
        if (frequency == ScheduleFrequency.Custom)
        {
            if (string.IsNullOrWhiteSpace(cron))
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("Cron expression is required when using Custom frequency.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                _logger.LogWarning("Schedule create failed: cron expression required for Custom frequency");
                return;
            }

            var (isValid, errorMessage) = await _scheduledMessageService.ValidateCronExpressionAsync(cron);
            if (!isValid)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Invalid Cron Expression")
                    .WithDescription($"The cron expression is invalid: {errorMessage}")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                _logger.LogWarning("Schedule create failed: invalid cron expression - {ErrorMessage}", errorMessage);
                return;
            }
        }

        // Calculate next execution time
        var nextExecution = await _scheduledMessageService.CalculateNextExecutionAsync(frequency, cron);
        if (!nextExecution.HasValue)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("Failed to calculate next execution time.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            _logger.LogError("Schedule create failed: could not calculate next execution time for frequency {Frequency}", frequency);
            return;
        }

        // Create the scheduled message
        var createDto = new ScheduledMessageCreateDto
        {
            GuildId = Context.Guild.Id,
            ChannelId = channel.Id,
            Title = title,
            Content = message,
            Frequency = frequency,
            CronExpression = cron,
            IsEnabled = true,
            NextExecutionAt = nextExecution.Value,
            CreatedBy = Context.User.Id.ToString()
        };

        try
        {
            var created = await _scheduledMessageService.CreateAsync(createDto);

            _logger.LogInformation(
                "Scheduled message created successfully: ID {MessageId}, Title: {Title}, Next execution: {NextExecution}",
                created.Id,
                created.Title,
                created.NextExecutionAt);

            var messagePreview = message.Length > 500 ? message.Substring(0, 497) + "..." : message;

            var embed = new EmbedBuilder()
                .WithTitle("✅ Scheduled Message Created")
                .WithColor(Color.Green)
                .AddField("Title", title, inline: true)
                .AddField("Channel", $"<#{channel.Id}>", inline: true)
                .AddField("Frequency", frequency == ScheduleFrequency.Custom ? $"Custom ({cron})" : frequency.ToString(), inline: true)
                .AddField("Next Execution", $"<t:{new DateTimeOffset(created.NextExecutionAt!.Value).ToUnixTimeSeconds()}:F>", inline: false)
                .AddField("Message Preview", messagePreview, inline: false)
                .AddField("Message ID", created.Id.ToString(), inline: false)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);

            _logger.LogDebug("Schedule create command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create scheduled message: {Title}", title);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to create scheduled message: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Deletes a scheduled message with confirmation.
    /// </summary>
    [SlashCommand("schedule-delete", "Delete a scheduled message")]
    public async Task DeleteAsync([Summary("id", "The ID of the scheduled message to delete")] string id)
    {
        _logger.LogInformation(
            "Schedule delete command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), Message ID: {MessageId}",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            Context.Guild.Id,
            id);

        if (!Guid.TryParse(id, out var messageId))
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("Invalid message ID format.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            _logger.LogWarning("Schedule delete failed: invalid GUID format - {Id}", id);
            return;
        }

        var message = await _scheduledMessageService.GetByIdAsync(messageId);
        if (message == null)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("Scheduled message not found.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            _logger.LogWarning("Schedule delete failed: message not found - {MessageId}", messageId);
            return;
        }

        // Verify the message belongs to this guild
        if (message.GuildId != Context.Guild.Id)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("This scheduled message does not belong to this guild.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            _logger.LogWarning(
                "Schedule delete failed: message {MessageId} belongs to guild {MessageGuildId}, not {CurrentGuildId}",
                messageId,
                message.GuildId,
                Context.Guild.Id);
            return;
        }

        // Create state for delete confirmation
        var state = new ScheduleDeleteState
        {
            MessageId = messageId,
            MessageTitle = message.Title
        };

        var correlationId = _stateService.CreateState(Context.User.Id, state);

        // Build confirmation buttons
        var confirmId = ComponentIdBuilder.Build("schedule", "delete-confirm", Context.User.Id, correlationId);
        var cancelId = ComponentIdBuilder.Build("schedule", "delete-cancel", Context.User.Id, correlationId);

        var components = new ComponentBuilder()
            .WithButton("Confirm Delete", confirmId, ButtonStyle.Danger)
            .WithButton("Cancel", cancelId, ButtonStyle.Secondary)
            .Build();

        var embed = new EmbedBuilder()
            .WithTitle("⚠️ Delete Confirmation")
            .WithDescription($"Are you sure you want to delete the scheduled message **{message.Title}**?\n\nThis action cannot be undone.")
            .WithColor(Color.Orange)
            .AddField("Message ID", messageId.ToString(), inline: true)
            .AddField("Frequency", message.Frequency.ToString(), inline: true)
            .WithCurrentTimestamp()
            .WithFooter("Admin Command")
            .Build();

        await RespondAsync(embed: embed, components: components, ephemeral: true);

        _logger.LogDebug("Schedule delete confirmation sent for message {MessageId}", messageId);
    }

    /// <summary>
    /// Toggles the enabled state of a scheduled message.
    /// </summary>
    [SlashCommand("schedule-toggle", "Enable or disable a scheduled message")]
    public async Task ToggleAsync([Summary("id", "The ID of the scheduled message to toggle")] string id)
    {
        _logger.LogInformation(
            "Schedule toggle command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), Message ID: {MessageId}",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            Context.Guild.Id,
            id);

        if (!Guid.TryParse(id, out var messageId))
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("Invalid message ID format.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            _logger.LogWarning("Schedule toggle failed: invalid GUID format - {Id}", id);
            return;
        }

        var message = await _scheduledMessageService.GetByIdAsync(messageId);
        if (message == null)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("Scheduled message not found.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            _logger.LogWarning("Schedule toggle failed: message not found - {MessageId}", messageId);
            return;
        }

        // Verify the message belongs to this guild
        if (message.GuildId != Context.Guild.Id)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("This scheduled message does not belong to this guild.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            _logger.LogWarning(
                "Schedule toggle failed: message {MessageId} belongs to guild {MessageGuildId}, not {CurrentGuildId}",
                messageId,
                message.GuildId,
                Context.Guild.Id);
            return;
        }

        // Toggle the enabled state
        var updateDto = new ScheduledMessageUpdateDto
        {
            IsEnabled = !message.IsEnabled
        };

        try
        {
            var updated = await _scheduledMessageService.UpdateAsync(messageId, updateDto);
            if (updated == null)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Error")
                    .WithDescription("Failed to update scheduled message.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                _logger.LogError("Schedule toggle failed: update returned null for message {MessageId}", messageId);
                return;
            }

            _logger.LogInformation(
                "Scheduled message {MessageId} toggled to {State} by user {UserId}",
                messageId,
                updated.IsEnabled ? "enabled" : "disabled",
                Context.User.Id);

            var statusIcon = updated.IsEnabled ? "✅" : "❌";
            var statusText = updated.IsEnabled ? "Enabled" : "Disabled";

            var embed = new EmbedBuilder()
                .WithTitle($"{statusIcon} Scheduled Message {statusText}")
                .WithDescription($"The scheduled message **{updated.Title}** has been {statusText.ToLower()}.")
                .WithColor(updated.IsEnabled ? Color.Green : Color.Orange)
                .AddField("Message ID", messageId.ToString(), inline: true)
                .AddField("Status", statusText, inline: true)
                .WithCurrentTimestamp()
                .WithFooter("Admin Command")
                .Build();

            await RespondAsync(embed: embed, ephemeral: true);

            _logger.LogDebug("Schedule toggle command response sent successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle scheduled message {MessageId}", messageId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to toggle scheduled message: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Executes a scheduled message immediately.
    /// </summary>
    [SlashCommand("schedule-run", "Execute a scheduled message immediately")]
    public async Task RunAsync([Summary("id", "The ID of the scheduled message to execute")] string id)
    {
        _logger.LogInformation(
            "Schedule run command executed by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId}), Message ID: {MessageId}",
            Context.User.Username,
            Context.User.Id,
            Context.Guild.Name,
            Context.Guild.Id,
            id);

        if (!Guid.TryParse(id, out var messageId))
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("Invalid message ID format.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            _logger.LogWarning("Schedule run failed: invalid GUID format - {Id}", id);
            return;
        }

        var message = await _scheduledMessageService.GetByIdAsync(messageId);
        if (message == null)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("Scheduled message not found.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            _logger.LogWarning("Schedule run failed: message not found - {MessageId}", messageId);
            return;
        }

        // Verify the message belongs to this guild
        if (message.GuildId != Context.Guild.Id)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription("This scheduled message does not belong to this guild.")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
            _logger.LogWarning(
                "Schedule run failed: message {MessageId} belongs to guild {MessageGuildId}, not {CurrentGuildId}",
                messageId,
                message.GuildId,
                Context.Guild.Id);
            return;
        }

        try
        {
            var success = await _scheduledMessageService.ExecuteScheduledMessageAsync(messageId);

            if (success)
            {
                _logger.LogInformation(
                    "Scheduled message {MessageId} executed successfully by user {UserId}",
                    messageId,
                    Context.User.Id);

                var embed = new EmbedBuilder()
                    .WithTitle("✅ Message Executed")
                    .WithDescription($"The scheduled message **{message.Title}** has been executed successfully.")
                    .WithColor(Color.Green)
                    .AddField("Message ID", messageId.ToString(), inline: true)
                    .AddField("Channel", $"<#{message.ChannelId}>", inline: true)
                    .WithCurrentTimestamp()
                    .WithFooter("Admin Command")
                    .Build();

                await RespondAsync(embed: embed, ephemeral: true);

                _logger.LogDebug("Schedule run command response sent successfully");
            }
            else
            {
                _logger.LogError("Failed to execute scheduled message {MessageId}", messageId);

                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Execution Failed")
                    .WithDescription("Failed to execute the scheduled message. The message may not be found or the channel may be inaccessible.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while executing scheduled message {MessageId}", messageId);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"An error occurred while executing the scheduled message: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }
}
