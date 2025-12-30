using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Component interaction handlers for Rat Watch buttons.
/// </summary>
[RequireGuildActive]
[RequireRatWatchEnabled]
public class RatWatchComponentModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IRatWatchService _ratWatchService;
    private readonly ILogger<RatWatchComponentModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RatWatchComponentModule"/> class.
    /// </summary>
    public RatWatchComponentModule(
        IRatWatchService ratWatchService,
        ILogger<RatWatchComponentModule> logger)
    {
        _ratWatchService = ratWatchService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the "I'm Here!" early check-in button.
    /// </summary>
    [ComponentInteraction("ratwatch:checkin:*:*:")]
    public async Task HandleCheckInAsync()
    {
        var customId = ((SocketMessageComponent)Context.Interaction).Data.CustomId;

        _logger.LogDebug(
            "Rat Watch check-in button clicked by {Username} (ID: {UserId}) with custom ID {CustomId}",
            Context.User.Username,
            Context.User.Id,
            customId);

        if (!ComponentIdBuilder.TryParse(customId, out var parts))
        {
            await RespondAsync("This button is invalid or expired.", ephemeral: true);
            _logger.LogWarning("Invalid component ID format: {CustomId}", customId);
            return;
        }

        // Validate user is the accused
        if (parts.UserId != Context.User.Id)
        {
            await RespondAsync("Only the accused user can check in.", ephemeral: true);
            _logger.LogDebug(
                "User {ActualUserId} attempted to check in for user {ExpectedUserId}",
                Context.User.Id,
                parts.UserId);
            return;
        }

        // Parse watchId from correlationId
        if (!Guid.TryParse(parts.CorrelationId, out var watchId))
        {
            await RespondAsync("This button is invalid.", ephemeral: true);
            _logger.LogWarning("Invalid watch ID format: {CorrelationId}", parts.CorrelationId);
            return;
        }

        try
        {
            // Clear the watch
            var success = await _ratWatchService.ClearWatchAsync(watchId, Context.User.Id);

            if (success)
            {
                _logger.LogInformation(
                    "Rat Watch {WatchId} cleared early by user {UserId} ({Username})",
                    watchId,
                    Context.User.Id,
                    Context.User.Username);

                // Update the message to show cleared status
                var component = (SocketMessageComponent)Context.Interaction;
                await component.UpdateAsync(props =>
                {
                    props.Content = $"✅ {Context.User.Mention} checked in! Watch cleared.";
                    props.Components = new ComponentBuilder()
                        .WithButton("Checked In ✓", "disabled_checkin", ButtonStyle.Success, disabled: true)
                        .Build();
                });

                _logger.LogDebug("Rat Watch check-in message updated successfully");
            }
            else
            {
                await RespondAsync("Could not check in. The watch may have already been processed.", ephemeral: true);
                _logger.LogWarning(
                    "Failed to clear Rat Watch {WatchId} for user {UserId} - watch not found or already processed",
                    watchId,
                    Context.User.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear Rat Watch {WatchId} for user {UserId}", watchId, Context.User.Id);

            await RespondAsync("An error occurred while checking in. Please try again.", ephemeral: true);
        }
    }

    /// <summary>
    /// Handles voting buttons (guilty/not guilty).
    /// Button ID format: ratwatch:vote:{accusedUserId}:{watchId}:{guilty|notguilty}
    /// </summary>
    [ComponentInteraction("ratwatch:vote:*:*:*")]
    public async Task HandleVoteAsync()
    {
        var customId = ((SocketMessageComponent)Context.Interaction).Data.CustomId;

        _logger.LogDebug(
            "Rat Watch vote button clicked by {Username} (ID: {UserId}) with custom ID {CustomId}",
            Context.User.Username,
            Context.User.Id,
            customId);

        if (!ComponentIdBuilder.TryParse(customId, out var parts))
        {
            await RespondAsync("This button is invalid or expired.", ephemeral: true);
            _logger.LogWarning("Invalid component ID format: {CustomId}", customId);
            return;
        }

        // Parse watchId and vote type
        if (!Guid.TryParse(parts.CorrelationId, out var watchId))
        {
            await RespondAsync("This button is invalid.", ephemeral: true);
            _logger.LogWarning("Invalid watch ID format: {CorrelationId}", parts.CorrelationId);
            return;
        }

        if (string.IsNullOrWhiteSpace(parts.Data))
        {
            await RespondAsync("This button is invalid.", ephemeral: true);
            _logger.LogWarning("Missing vote type in Data field for custom ID {CustomId}", customId);
            return;
        }

        var isGuilty = parts.Data == "guilty";

        // Don't let the accused vote on their own watch
        if (parts.UserId == Context.User.Id)
        {
            await RespondAsync("You cannot vote on your own Rat Watch!", ephemeral: true);
            _logger.LogDebug(
                "User {UserId} attempted to vote on their own Rat Watch {WatchId}",
                Context.User.Id,
                watchId);
            return;
        }

        try
        {
            // Cast vote
            var success = await _ratWatchService.CastVoteAsync(watchId, Context.User.Id, isGuilty);

            if (success)
            {
                var voteType = isGuilty ? "🐀 Rat" : "✓ Not Rat";

                _logger.LogInformation(
                    "Vote cast on Rat Watch {WatchId} by user {UserId} ({Username}): {VoteType}",
                    watchId,
                    Context.User.Id,
                    Context.User.Username,
                    voteType);

                await RespondAsync($"Your vote ({voteType}) has been recorded!", ephemeral: true);

                _logger.LogDebug("Rat Watch vote response sent successfully");
            }
            else
            {
                await RespondAsync("Could not record your vote. Voting may have ended.", ephemeral: true);
                _logger.LogWarning(
                    "Failed to cast vote on Rat Watch {WatchId} for user {UserId} - watch not found or not in voting status",
                    watchId,
                    Context.User.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cast vote on Rat Watch {WatchId} for user {UserId}", watchId, Context.User.Id);

            await RespondAsync("An error occurred while recording your vote. Please try again.", ephemeral: true);
        }
    }
}
