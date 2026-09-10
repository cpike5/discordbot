using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Bot.Services.Moderation;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash commands for direct moderation actions (warn, kick, ban, unban, mute, purge).
/// The warn/kick/ban/unban/mute commands delegate their shared validate/act/notify/case pipeline
/// to <see cref="IModerationActionRunner"/>; this module builds the request and renders the reply.
/// </summary>
[RequireGuildActive]
[RequireModerationEnabled]
[RequireModerator]
public class ModerationActionModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IModerationService _moderationService;
    private readonly IModerationActionRunner _actionRunner;
    private readonly ILogger<ModerationActionModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModerationActionModule"/> class.
    /// </summary>
    public ModerationActionModule(
        IModerationService moderationService,
        IModerationActionRunner actionRunner,
        ILogger<ModerationActionModule> logger)
    {
        _moderationService = moderationService;
        _actionRunner = actionRunner;
        _logger = logger;
    }

    /// <summary>
    /// Wraps <see cref="InteractionModuleBase{TInteractionContext}.Context"/> for the shared moderation pipeline.
    /// </summary>
    private IModerationCommandContext ActionContext => new InteractionModerationCommandContext(Context, _logger);

    /// <summary>
    /// Sends a <see cref="ModerationActionResult"/> as the initial interaction response.
    /// </summary>
    private async Task RespondWithResultAsync(ModerationActionResult result)
    {
        if (result.PlainText != null)
        {
            await RespondAsync(result.PlainText, ephemeral: result.Ephemeral);
        }
        else
        {
            await RespondAsync(embed: result.Embed, ephemeral: result.Ephemeral);
        }
    }

    /// <summary>
    /// Message context menu command to warn a user about a specific message.
    /// </summary>
    [MessageCommand("Warn User")]
    public async Task WarnUserMessageAsync(IMessage message)
    {
        var targetUser = message.Author;
        var userId = Context.User.Id;

        _logger.LogInformation(
            "Warn User message command executed by {ModeratorUsername} (ID: {ModeratorId}) for message {MessageId} by {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            userId,
            message.Id,
            targetUser.Username,
            targetUser.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        // Prevent warning bots (with admin exception for testing)
        if (targetUser.IsBot)
        {
            var guildUser = Context.User as SocketGuildUser;
            var isAdmin = guildUser?.GuildPermissions.Administrator ?? false;
            if (!isAdmin)
            {
                await RespondAsync(embed: EmbedHelper.Error("Cannot Warn Bot", "You cannot warn a bot user."), ephemeral: true);
                _logger.LogDebug("User {UserId} attempted to warn bot {BotId}", userId, targetUser.Id);
                return;
            }
        }

        // Prevent self-warning
        if (targetUser.Id == userId)
        {
            await RespondAsync(embed: EmbedHelper.Error("Cannot Warn Yourself", "You cannot warn yourself."), ephemeral: true);
            _logger.LogDebug("User {UserId} attempted to warn themselves", userId);
            return;
        }

        // Build modal with custom ID: handler:action:messageId:targetUserId:channelId
        var modalId = $"warnuser:submit:{message.Id}:{targetUser.Id}:{message.Channel.Id}";

        var modal = new ModalBuilder()
            .WithTitle("Warn User")
            .WithCustomId(modalId)
            .AddTextInput(
                "Reason (optional)",
                "reason",
                TextInputStyle.Paragraph,
                "Enter the reason for this warning...",
                maxLength: 500,
                required: false)
            .Build();

        await RespondWithModalAsync(modal);
        _logger.LogDebug("Warn User modal displayed to moderator {ModeratorId}", userId);
    }

    /// <summary>
    /// Modal handler for Warn User context menu command.
    /// </summary>
    [ModalInteraction("warnuser:submit:*:*:*")]
    public async Task HandleWarnUserModalAsync(string messageId, string targetUserId, string channelId, WarnUserModal modal)
    {
        _logger.LogInformation(
            "Warn User modal submitted by {ModeratorUsername} (ID: {ModeratorId}) for message {MessageId}, target user {TargetId}",
            Context.User.Username,
            Context.User.Id,
            messageId,
            targetUserId);

        // Parse IDs
        if (!ulong.TryParse(messageId, out var parsedMessageId) ||
            !ulong.TryParse(targetUserId, out var parsedTargetUserId) ||
            !ulong.TryParse(channelId, out var parsedChannelId))
        {
            await RespondAsync("Failed to parse message or user identifiers.", ephemeral: true);
            _logger.LogError("Failed to parse IDs from modal: messageId={MessageId}, targetUserId={TargetId}, channelId={ChannelId}",
                messageId, targetUserId, channelId);
            return;
        }

        await DeferAsync(ephemeral: false);

        try
        {
            // Fetch the message to get its content and construct jump URL
            var channel = await Context.Client.GetChannelAsync(parsedChannelId) as IMessageChannel;
            if (channel == null)
            {
                await FollowupAsync("Could not access the channel where the message was posted.", ephemeral: true);
                _logger.LogWarning("Could not resolve channel {ChannelId}", parsedChannelId);
                return;
            }

            var message = await channel.GetMessageAsync(parsedMessageId);
            if (message == null)
            {
                await FollowupAsync("Could not find the original message. It may have been deleted.", ephemeral: true);
                _logger.LogWarning("Could not resolve message {MessageId} in channel {ChannelId}", parsedMessageId, parsedChannelId);
                return;
            }

            // Truncate message content to 500 characters
            var messageContent = message.Content;
            if (messageContent.Length > 500)
            {
                messageContent = messageContent.Substring(0, 497) + "...";
            }

            var messageJumpUrl = message.GetJumpUrl();

            // Fetch target user
            var targetUser = await Context.Client.GetUserAsync(parsedTargetUserId);
            if (targetUser == null)
            {
                await FollowupAsync("Could not find the target user.", ephemeral: true);
                _logger.LogWarning("Could not resolve target user {UserId}", parsedTargetUserId);
                return;
            }

            // Create moderation case with message context
            var createDto = new ModerationCaseCreateDto
            {
                GuildId = Context.Guild.Id,
                TargetUserId = parsedTargetUserId,
                ModeratorUserId = Context.User.Id,
                Type = CaseType.Warn,
                Reason = string.IsNullOrWhiteSpace(modal.Reason) ? null : modal.Reason,
                ContextMessageId = parsedMessageId,
                ContextChannelId = parsedChannelId,
                ContextMessageContent = messageContent
            };

            var caseDto = await _moderationService.CreateCaseAsync(createDto);

            _logger.LogInformation(
                "Warning issued via message context: Case #{CaseNumber} for user {TargetId} by moderator {ModeratorId}, message {MessageId}",
                caseDto.CaseNumber,
                parsedTargetUserId,
                Context.User.Id,
                parsedMessageId);

            // Try to DM the user about the warning
            try
            {
                var dmEmbedBuilder = new EmbedBuilder()
                    .WithTitle($"⚠️ Warning in {Context.Guild.Name}")
                    .WithDescription("You have received a formal warning regarding a message you posted.")
                    .AddField("Case Number", $"#{caseDto.CaseNumber}", inline: true)
                    .AddField("Moderator", Context.User.Username, inline: true)
                    .WithColor(Color.Gold)
                    .WithCurrentTimestamp();

                if (!string.IsNullOrWhiteSpace(modal.Reason))
                {
                    dmEmbedBuilder.AddField("Reason", modal.Reason, inline: false);
                }

                // Add message context
                if (!string.IsNullOrWhiteSpace(messageContent))
                {
                    dmEmbedBuilder.AddField("Your Message", $">>> {messageContent}", inline: false);
                }

                dmEmbedBuilder.AddField("Message Link", $"[Jump to Message]({messageJumpUrl})", inline: false);

                await targetUser.SendMessageAsync(embed: dmEmbedBuilder.Build());
                _logger.LogDebug("Warning DM sent successfully to user {UserId}", parsedTargetUserId);
            }
            catch (Exception dmEx)
            {
                _logger.LogWarning(dmEx, "Failed to send warning DM to user {UserId}", parsedTargetUserId);
            }

            // Send confirmation embed in channel
            var confirmEmbedBuilder = new EmbedBuilder()
                .WithTitle("⚠️ Warning Issued")
                .WithColor(Color.Gold)
                .AddField("User", $"{targetUser.Mention} ({targetUser.Id})", inline: true)
                .AddField("Case", $"#{caseDto.CaseNumber}", inline: true)
                .AddField("Moderator", Context.User.Mention, inline: true)
                .WithCurrentTimestamp();

            if (!string.IsNullOrWhiteSpace(modal.Reason))
            {
                confirmEmbedBuilder.AddField("Reason", modal.Reason, inline: false);
            }

            // Add message context preview
            if (!string.IsNullOrWhiteSpace(messageContent))
            {
                confirmEmbedBuilder.AddField("Message Context", $">>> {messageContent}", inline: false);
            }

            confirmEmbedBuilder.AddField("Message Link", $"[Jump to Message]({messageJumpUrl})", inline: false);

            await FollowupAsync(embed: confirmEmbedBuilder.Build());

            _logger.LogDebug("Warn User command completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warn user {UserId} via message context", parsedTargetUserId);

            await FollowupAsync(embed: EmbedHelper.Error("Error", $"Failed to issue warning: {ex.Message}"), ephemeral: true);
        }
    }

    /// <summary>
    /// Issue a formal warning to a user.
    /// </summary>
    [SlashCommand("warn", "Issue a formal warning to a user")]
    public async Task WarnAsync(
        [Summary("user", "The user to warn")] IUser user,
        [Summary("reason", "Reason for the warning")] string? reason = null)
    {
        var result = await _actionRunner.WarnAsync(ActionContext, user, reason);
        await RespondWithResultAsync(result);
    }

    /// <summary>
    /// Kick a user from the server.
    /// </summary>
    [SlashCommand("kick", "Kick a user from the server")]
    [RequireKickMembers]
    [RequireBotPermission(GuildPermission.KickMembers)]
    public async Task KickAsync(
        [Summary("user", "The user to kick")] IUser user,
        [Summary("reason", "Reason for the kick")] string? reason = null)
    {
        var result = await _actionRunner.KickAsync(ActionContext, user, reason);
        await RespondWithResultAsync(result);
    }

    /// <summary>
    /// Ban a user from the server.
    /// </summary>
    [SlashCommand("ban", "Ban a user from the server")]
    [RequireBanMembers]
    [RequireBotPermission(GuildPermission.BanMembers)]
    public async Task BanAsync(
        [Summary("user", "The user to ban")] IUser user,
        [Summary("duration", "Ban duration (e.g., '7d', '24h'). Leave empty for permanent")] string? duration = null,
        [Summary("reason", "Reason for the ban")] string? reason = null,
        [Summary("delete_messages", "Days of messages to delete (0-7)")]
        [MinValue(0), MaxValue(7)] int deleteMessageDays = 0)
    {
        var result = await _actionRunner.BanAsync(ActionContext, user, duration, reason, deleteMessageDays);
        await RespondWithResultAsync(result);
    }

    /// <summary>
    /// Unban a user from the server.
    /// </summary>
    [SlashCommand("unban", "Unban a user from the server")]
    [RequireBanMembers]
    [RequireBotPermission(GuildPermission.BanMembers)]
    public async Task UnbanAsync(
        [Summary("user_id", "The ID of the banned user")] string userId,
        [Summary("reason", "Reason for the unban")] string? reason = null)
    {
        var result = await _actionRunner.UnbanAsync(ActionContext, userId, reason);
        await RespondWithResultAsync(result);
    }

    /// <summary>
    /// Timeout/mute a user.
    /// </summary>
    [SlashCommand("mute", "Timeout a user")]
    [RequireUserPermission(GuildPermission.ModerateMembers)]
    [RequireBotPermission(GuildPermission.ModerateMembers)]
    public async Task MuteAsync(
        [Summary("user", "The user to mute")] IUser user,
        [Summary("duration", "Mute duration (e.g., '10m', '1h', '1d')")] string duration,
        [Summary("reason", "Reason for the mute")] string? reason = null)
    {
        var result = await _actionRunner.MuteAsync(ActionContext, user, duration, reason);
        await RespondWithResultAsync(result);
    }

    /// <summary>
    /// Bulk delete messages from a channel.
    /// </summary>
    [SlashCommand("purge", "Bulk delete messages")]
    [RequireUserPermission(ChannelPermission.ManageMessages)]
    [RequireBotPermission(ChannelPermission.ManageMessages)]
    public async Task PurgeAsync(
        [Summary("count", "Number of messages to delete (1-100)")]
        [MinValue(1), MaxValue(100)] int count,
        [Summary("user", "Only delete messages from this user")] IUser? user = null)
    {
        _logger.LogInformation(
            "Purge command executed by {ModeratorUsername} (ID: {ModeratorId}) in channel {ChannelId}, count: {Count}, user filter: {UserFilter}",
            Context.User.Username,
            Context.User.Id,
            Context.Channel.Id,
            count,
            user?.Id.ToString() ?? "none");

        await DeferAsync(ephemeral: true);

        try
        {
            // Get messages from channel
            var channel = Context.Channel as ITextChannel;
            if (channel == null)
            {
                await FollowupAsync("This command can only be used in a text channel.", ephemeral: true);
                return;
            }

            // Fetch messages (Discord.NET limit is 100)
            var messages = await channel.GetMessagesAsync(count + 1).FlattenAsync(); // +1 to include command invocation

            // Filter out messages older than 14 days (Discord API limitation)
            var twoWeeksAgo = DateTimeOffset.UtcNow.AddDays(-14);
            messages = messages.Where(m => m.CreatedAt > twoWeeksAgo);

            // Filter by user if specified
            if (user != null)
            {
                messages = messages.Where(m => m.Author.Id == user.Id);
            }

            var messageList = messages.ToList();

            if (messageList.Count == 0)
            {
                await FollowupAsync("No messages found to delete.", ephemeral: true);
                _logger.LogDebug("No messages found matching purge criteria");
                return;
            }

            // Delete messages using bulk delete
            await channel.DeleteMessagesAsync(messageList);

            _logger.LogInformation(
                "Purged {DeletedCount} messages from channel {ChannelId} by moderator {ModeratorId}",
                messageList.Count,
                Context.Channel.Id,
                Context.User.Id);

            // Send ephemeral confirmation
            var confirmationMessage = user != null
                ? $"✅ Successfully deleted {messageList.Count} message(s) from {user.Username}."
                : $"✅ Successfully deleted {messageList.Count} message(s).";

            await FollowupAsync(confirmationMessage, ephemeral: true);

            _logger.LogDebug("Purge command completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge messages from channel {ChannelId}", Context.Channel.Id);

            await FollowupAsync($"Failed to purge messages: {ex.Message}", ephemeral: true);
        }
    }

}

/// <summary>
/// Modal data for Warn User context menu command.
/// </summary>
public class WarnUserModal : IModal
{
    public string Title => "Warn User";

    [InputLabel("Reason (optional)")]
    [ModalTextInput("reason", TextInputStyle.Paragraph, "Enter the reason for this warning...", maxLength: 500)]
    public string? Reason { get; set; }
}
