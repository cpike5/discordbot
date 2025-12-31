using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Component interaction handlers for auto-moderation flagged event alert buttons.
/// </summary>
public class FlaggedEventComponentModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IFlaggedEventService _flaggedEventService;
    private readonly ILogger<FlaggedEventComponentModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlaggedEventComponentModule"/> class.
    /// </summary>
    public FlaggedEventComponentModule(
        IFlaggedEventService flaggedEventService,
        ILogger<FlaggedEventComponentModule> logger)
    {
        _flaggedEventService = flaggedEventService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the "Dismiss" button on auto-mod alerts.
    /// Button ID format: automod:dismiss:{eventId}
    /// </summary>
    [ComponentInteraction("automod:dismiss:*")]
    public async Task HandleDismissAsync(string eventIdStr)
    {
        _logger.LogDebug(
            "Dismiss button clicked by {Username} (ID: {UserId}) for event {EventId}",
            Context.User.Username,
            Context.User.Id,
            eventIdStr);

        if (!Guid.TryParse(eventIdStr, out var eventId))
        {
            await RespondAsync("Invalid event ID.", ephemeral: true);
            _logger.LogWarning("Invalid event ID format: {EventId}", eventIdStr);
            return;
        }

        try
        {
            var updatedEvent = await _flaggedEventService.DismissEventAsync(eventId, Context.User.Id);

            if (updatedEvent == null)
            {
                await RespondAsync("Event not found or already processed.", ephemeral: true);
                _logger.LogWarning(
                    "Failed to dismiss event {EventId}: event not found",
                    eventId);
                return;
            }

            _logger.LogInformation(
                "Flagged event {EventId} dismissed by user {UserId} ({Username})",
                eventId,
                Context.User.Id,
                Context.User.Username);

            // Update the message to show dismissed status
            var component = (SocketMessageComponent)Context.Interaction;
            await component.UpdateAsync(props =>
            {
                var originalEmbed = component.Message.Embeds.FirstOrDefault();
                if (originalEmbed != null)
                {
                    var updatedEmbed = new EmbedBuilder()
                        .WithTitle(originalEmbed.Title)
                        .WithDescription(originalEmbed.Description)
                        .WithColor(Color.DarkGrey)
                        .WithFooter($"Dismissed by {Context.User.Username} • Event ID: {eventId}")
                        .WithTimestamp(DateTimeOffset.UtcNow);

                    // Copy fields from original embed
                    foreach (var field in originalEmbed.Fields)
                    {
                        updatedEmbed.AddField(field.Name, field.Value, field.Inline);
                    }

                    props.Embed = updatedEmbed.Build();
                }

                props.Components = new ComponentBuilder()
                    .WithButton("Dismissed", "disabled_dismiss", ButtonStyle.Secondary, disabled: true)
                    .Build();
            });

            _logger.LogDebug("Flagged event alert message updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dismiss flagged event {EventId}", eventId);
            await RespondAsync("An error occurred while dismissing the event. Please try again.", ephemeral: true);
        }
    }

    /// <summary>
    /// Handles the "Acknowledge" button on auto-mod alerts.
    /// Button ID format: automod:ack:{eventId}
    /// </summary>
    [ComponentInteraction("automod:ack:*")]
    public async Task HandleAcknowledgeAsync(string eventIdStr)
    {
        _logger.LogDebug(
            "Acknowledge button clicked by {Username} (ID: {UserId}) for event {EventId}",
            Context.User.Username,
            Context.User.Id,
            eventIdStr);

        if (!Guid.TryParse(eventIdStr, out var eventId))
        {
            await RespondAsync("Invalid event ID.", ephemeral: true);
            _logger.LogWarning("Invalid event ID format: {EventId}", eventIdStr);
            return;
        }

        try
        {
            var updatedEvent = await _flaggedEventService.AcknowledgeEventAsync(eventId, Context.User.Id);

            if (updatedEvent == null)
            {
                await RespondAsync("Event not found or already processed.", ephemeral: true);
                _logger.LogWarning(
                    "Failed to acknowledge event {EventId}: event not found",
                    eventId);
                return;
            }

            _logger.LogInformation(
                "Flagged event {EventId} acknowledged by user {UserId} ({Username})",
                eventId,
                Context.User.Id,
                Context.User.Username);

            // Update the message to show acknowledged status
            var component = (SocketMessageComponent)Context.Interaction;
            await component.UpdateAsync(props =>
            {
                var originalEmbed = component.Message.Embeds.FirstOrDefault();
                if (originalEmbed != null)
                {
                    var updatedEmbed = new EmbedBuilder()
                        .WithTitle(originalEmbed.Title)
                        .WithDescription(originalEmbed.Description)
                        .WithColor(Color.Blue)
                        .WithFooter($"Acknowledged by {Context.User.Username} • Event ID: {eventId}")
                        .WithTimestamp(DateTimeOffset.UtcNow);

                    // Copy fields from original embed
                    foreach (var field in originalEmbed.Fields)
                    {
                        updatedEmbed.AddField(field.Name, field.Value, field.Inline);
                    }

                    props.Embed = updatedEmbed.Build();
                }

                props.Components = new ComponentBuilder()
                    .WithButton("Acknowledged", "disabled_ack", ButtonStyle.Primary, disabled: true)
                    .Build();
            });

            _logger.LogDebug("Flagged event alert message updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge flagged event {EventId}", eventId);
            await RespondAsync("An error occurred while acknowledging the event. Please try again.", ephemeral: true);
        }
    }

    /// <summary>
    /// Handles the "Take Action" button on auto-mod alerts.
    /// Button ID format: automod:action:{eventId}
    /// </summary>
    [ComponentInteraction("automod:action:*")]
    public async Task HandleActionAsync(string eventIdStr)
    {
        _logger.LogDebug(
            "Take Action button clicked by {Username} (ID: {UserId}) for event {EventId}",
            Context.User.Username,
            Context.User.Id,
            eventIdStr);

        if (!Guid.TryParse(eventIdStr, out var eventId))
        {
            await RespondAsync("Invalid event ID.", ephemeral: true);
            _logger.LogWarning("Invalid event ID format: {EventId}", eventIdStr);
            return;
        }

        try
        {
            // Get the event details
            var flaggedEvent = await _flaggedEventService.GetEventAsync(eventId);

            if (flaggedEvent == null)
            {
                await RespondAsync("Event not found.", ephemeral: true);
                _logger.LogWarning("Event {EventId} not found", eventId);
                return;
            }

            // Show action selection menu
            var selectMenu = new SelectMenuBuilder()
                .WithPlaceholder("Select moderation action...")
                .WithCustomId($"automod:actionselect:{eventId}")
                .AddOption("Warn User", "warn", "Issue a formal warning")
                .AddOption("Mute User (1 hour)", "mute", "Timeout user for 1 hour")
                .AddOption("Kick User", "kick", "Remove user from server")
                .AddOption("Ban User", "ban", "Permanently ban user from server")
                .AddOption("Add to Watchlist", "watchlist", "Add user to moderator watchlist")
                .AddOption("No Action Needed", "none", "Mark as actioned without taking moderation action");

            var components = new ComponentBuilder()
                .WithSelectMenu(selectMenu)
                .Build();

            await RespondAsync(
                $"Choose a moderation action for this event:\n" +
                $"**User:** <@{flaggedEvent.UserId}>\n" +
                $"**Type:** {flaggedEvent.RuleType}\n" +
                $"**Severity:** {flaggedEvent.Severity}\n" +
                $"**Description:** {flaggedEvent.Description}",
                components: components,
                ephemeral: true);

            _logger.LogDebug("Action selection menu displayed for event {EventId}", eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to show action menu for flagged event {EventId}", eventId);
            await RespondAsync("An error occurred while loading the action menu. Please try again.", ephemeral: true);
        }
    }

    /// <summary>
    /// Handles the action selection dropdown for taking action on flagged events.
    /// Menu ID format: automod:actionselect:{eventId}
    /// </summary>
    [ComponentInteraction("automod:actionselect:*")]
    public async Task HandleActionSelectAsync(string eventIdStr, string[] selectedActions)
    {
        _logger.LogDebug(
            "Action selected by {Username} (ID: {UserId}) for event {EventId}: {Action}",
            Context.User.Username,
            Context.User.Id,
            eventIdStr,
            selectedActions.FirstOrDefault());

        if (!Guid.TryParse(eventIdStr, out var eventId))
        {
            await RespondAsync("Invalid event ID.", ephemeral: true);
            _logger.LogWarning("Invalid event ID format: {EventId}", eventIdStr);
            return;
        }

        var action = selectedActions.FirstOrDefault();
        if (string.IsNullOrEmpty(action))
        {
            await RespondAsync("No action selected.", ephemeral: true);
            return;
        }

        try
        {
            // Record the action taken
            var actionDescription = action switch
            {
                "warn" => "User warned",
                "mute" => "User muted for 1 hour",
                "kick" => "User kicked from server",
                "ban" => "User banned from server",
                "watchlist" => "User added to watchlist",
                "none" => "No action taken - marked as reviewed",
                _ => "Unknown action"
            };

            var updatedEvent = await _flaggedEventService.TakeActionAsync(
                eventId,
                actionDescription,
                Context.User.Id);

            if (updatedEvent == null)
            {
                await RespondAsync("Event not found or already processed.", ephemeral: true);
                _logger.LogWarning(
                    "Failed to record action for event {EventId}: event not found",
                    eventId);
                return;
            }

            _logger.LogInformation(
                "Action '{Action}' recorded for flagged event {EventId} by user {UserId} ({Username})",
                actionDescription,
                eventId,
                Context.User.Id,
                Context.User.Username);

            await RespondAsync(
                $"✅ Action recorded: {actionDescription}\n\n" +
                $"**Note:** This only records the action in the system. You must manually execute the moderation action " +
                $"(warn/mute/kick/ban) using the appropriate command or Discord's built-in moderation tools.",
                ephemeral: true);

            _logger.LogDebug("Action confirmation sent for event {EventId}", eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record action for flagged event {EventId}", eventId);
            await RespondAsync("An error occurred while recording the action. Please try again.", ephemeral: true);
        }
    }
}
