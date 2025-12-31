using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Bot.Utilities;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash commands for direct moderation actions (warn, kick, ban, mute, purge).
/// </summary>
[RequireGuildActive]
[RequireModerationEnabled]
[RequireModerator]
public class ModerationActionModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IModerationService _moderationService;
    private readonly ILogger<ModerationActionModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModerationActionModule"/> class.
    /// </summary>
    public ModerationActionModule(
        IModerationService moderationService,
        ILogger<ModerationActionModule> logger)
    {
        _moderationService = moderationService;
        _logger = logger;
    }

    /// <summary>
    /// Issue a formal warning to a user.
    /// </summary>
    [SlashCommand("warn", "Issue a formal warning to a user")]
    public async Task WarnAsync(
        [Summary("user", "The user to warn")] IUser user,
        [Summary("reason", "Reason for the warning")] string? reason = null)
    {
        _logger.LogInformation(
            "Warn command executed by {ModeratorUsername} (ID: {ModeratorId}) for user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            user.Username,
            user.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        // Prevent self-warning
        if (user.Id == Context.User.Id)
        {
            await RespondAsync("You cannot warn yourself.", ephemeral: true);
            _logger.LogDebug("User {UserId} attempted to warn themselves", Context.User.Id);
            return;
        }

        // Prevent warning the bot
        if (user.IsBot && user.Id == Context.Client.CurrentUser.Id)
        {
            await RespondAsync("I cannot be warned.", ephemeral: true);
            _logger.LogDebug("User {UserId} attempted to warn the bot", Context.User.Id);
            return;
        }

        try
        {
            // Create moderation case
            var createDto = new ModerationCaseCreateDto
            {
                GuildId = Context.Guild.Id,
                TargetUserId = user.Id,
                ModeratorUserId = Context.User.Id,
                Type = CaseType.Warn,
                Reason = reason
            };

            var caseDto = await _moderationService.CreateCaseAsync(createDto);

            _logger.LogInformation(
                "Warning issued: Case #{CaseNumber} for user {TargetId} by moderator {ModeratorId}",
                caseDto.CaseNumber,
                user.Id,
                Context.User.Id);

            // Try to DM the user about the warning
            try
            {
                var dmEmbed = new EmbedBuilder()
                    .WithTitle($"⚠️ Warning in {Context.Guild.Name}")
                    .WithDescription(string.IsNullOrWhiteSpace(reason)
                        ? "You have received a formal warning."
                        : $"**Reason:** {reason}")
                    .AddField("Case Number", $"#{caseDto.CaseNumber}", inline: true)
                    .AddField("Moderator", Context.User.Username, inline: true)
                    .WithColor(Color.Gold)
                    .WithCurrentTimestamp()
                    .Build();

                await user.SendMessageAsync(embed: dmEmbed);
                _logger.LogDebug("Warning DM sent successfully to user {UserId}", user.Id);
            }
            catch (Exception dmEx)
            {
                _logger.LogWarning(dmEx, "Failed to send warning DM to user {UserId}", user.Id);
            }

            // Send confirmation embed
            var confirmEmbed = BuildActionEmbed(
                "⚠️ Warning Issued",
                user,
                CaseType.Warn,
                caseDto.CaseNumber,
                reason);

            await RespondAsync(embed: confirmEmbed);

            _logger.LogDebug("Warn command completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warn user {UserId}", user.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to issue warning: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Kick a user from the server.
    /// </summary>
    [SlashCommand("kick", "Kick a user from the server")]
    [RequireKickMembers]
    [RequireBotPermission(GuildPermission.KickMembers)]
    public async Task KickAsync(
        [Summary("user", "The user to kick")] IGuildUser user,
        [Summary("reason", "Reason for the kick")] string? reason = null)
    {
        _logger.LogInformation(
            "Kick command executed by {ModeratorUsername} (ID: {ModeratorId}) for user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            user.Username,
            user.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        // Prevent self-kick
        if (user.Id == Context.User.Id)
        {
            await RespondAsync("You cannot kick yourself.", ephemeral: true);
            _logger.LogDebug("User {UserId} attempted to kick themselves", Context.User.Id);
            return;
        }

        // Prevent kicking the bot
        if (user.IsBot && user.Id == Context.Client.CurrentUser.Id)
        {
            await RespondAsync("I cannot kick myself.", ephemeral: true);
            _logger.LogDebug("User {UserId} attempted to kick the bot", Context.User.Id);
            return;
        }

        // Check role hierarchy
        if (Context.User is SocketGuildUser moderator && user.Hierarchy >= moderator.Hierarchy)
        {
            await RespondAsync("You cannot kick a user with an equal or higher role than yours.", ephemeral: true);
            _logger.LogDebug(
                "User {ModeratorId} attempted to kick user {TargetId} with equal/higher role hierarchy",
                Context.User.Id,
                user.Id);
            return;
        }

        try
        {
            // Create moderation case
            var createDto = new ModerationCaseCreateDto
            {
                GuildId = Context.Guild.Id,
                TargetUserId = user.Id,
                ModeratorUserId = Context.User.Id,
                Type = CaseType.Kick,
                Reason = reason
            };

            var caseDto = await _moderationService.CreateCaseAsync(createDto);

            _logger.LogInformation(
                "Kick case created: Case #{CaseNumber} for user {TargetId} by moderator {ModeratorId}",
                caseDto.CaseNumber,
                user.Id,
                Context.User.Id);

            // Try to DM the user before kicking
            try
            {
                var dmEmbed = new EmbedBuilder()
                    .WithTitle($"🥾 Kicked from {Context.Guild.Name}")
                    .WithDescription(string.IsNullOrWhiteSpace(reason)
                        ? "You have been kicked from the server."
                        : $"**Reason:** {reason}")
                    .AddField("Case Number", $"#{caseDto.CaseNumber}", inline: true)
                    .AddField("Moderator", Context.User.Username, inline: true)
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await user.SendMessageAsync(embed: dmEmbed);
                _logger.LogDebug("Kick DM sent successfully to user {UserId}", user.Id);
            }
            catch (Exception dmEx)
            {
                _logger.LogWarning(dmEx, "Failed to send kick DM to user {UserId}", user.Id);
            }

            // Kick the user using Discord API
            await user.KickAsync(reason);
            _logger.LogInformation("User {UserId} kicked from guild {GuildId}", user.Id, Context.Guild.Id);

            // Send confirmation embed
            var confirmEmbed = BuildActionEmbed(
                "🥾 User Kicked",
                user,
                CaseType.Kick,
                caseDto.CaseNumber,
                reason);

            await RespondAsync(embed: confirmEmbed);

            _logger.LogDebug("Kick command completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to kick user {UserId}", user.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to kick user: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
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
        _logger.LogInformation(
            "Ban command executed by {ModeratorUsername} (ID: {ModeratorId}) for user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId}), duration: {Duration}",
            Context.User.Username,
            Context.User.Id,
            user.Username,
            user.Id,
            Context.Guild.Name,
            Context.Guild.Id,
            duration ?? "permanent");

        // Prevent self-ban
        if (user.Id == Context.User.Id)
        {
            await RespondAsync("You cannot ban yourself.", ephemeral: true);
            _logger.LogDebug("User {UserId} attempted to ban themselves", Context.User.Id);
            return;
        }

        // Prevent banning the bot
        if (user.IsBot && user.Id == Context.Client.CurrentUser.Id)
        {
            await RespondAsync("I cannot ban myself.", ephemeral: true);
            _logger.LogDebug("User {UserId} attempted to ban the bot", Context.User.Id);
            return;
        }

        // Check role hierarchy if user is in guild
        if (user is IGuildUser guildUser && Context.User is SocketGuildUser moderator)
        {
            if (guildUser.Hierarchy >= moderator.Hierarchy)
            {
                await RespondAsync("You cannot ban a user with an equal or higher role than yours.", ephemeral: true);
                _logger.LogDebug(
                    "User {ModeratorId} attempted to ban user {TargetId} with equal/higher role hierarchy",
                    Context.User.Id,
                    user.Id);
                return;
            }
        }

        try
        {
            // Parse duration if provided
            TimeSpan? parsedDuration = null;
            if (!string.IsNullOrWhiteSpace(duration))
            {
                parsedDuration = DurationParser.Parse(duration);
                if (!parsedDuration.HasValue)
                {
                    var errorEmbed = new EmbedBuilder()
                        .WithTitle("❌ Invalid Duration Format")
                        .WithDescription("Could not parse the duration you provided. Use formats like:\n• `7d` - 7 days\n• `24h` - 24 hours\n• `1h30m` - 1 hour 30 minutes")
                        .WithColor(Color.Red)
                        .WithCurrentTimestamp()
                        .Build();

                    await RespondAsync(embed: errorEmbed, ephemeral: true);
                    _logger.LogDebug("Failed to parse ban duration input: {DurationInput}", duration);
                    return;
                }

                _logger.LogDebug("Parsed ban duration: {Duration}", parsedDuration.Value);
            }

            // Create moderation case
            var createDto = new ModerationCaseCreateDto
            {
                GuildId = Context.Guild.Id,
                TargetUserId = user.Id,
                ModeratorUserId = Context.User.Id,
                Type = CaseType.Ban,
                Reason = reason,
                Duration = parsedDuration
            };

            var caseDto = await _moderationService.CreateCaseAsync(createDto);

            _logger.LogInformation(
                "Ban case created: Case #{CaseNumber} for user {TargetId} by moderator {ModeratorId}, expires: {ExpiresAt}",
                caseDto.CaseNumber,
                user.Id,
                Context.User.Id,
                caseDto.ExpiresAt?.ToString() ?? "never");

            // Try to DM the user before banning
            try
            {
                var dmDescription = parsedDuration.HasValue
                    ? $"You have been temporarily banned for {DurationParser.Format(parsedDuration.Value)}."
                    : "You have been permanently banned from the server.";

                if (!string.IsNullOrWhiteSpace(reason))
                {
                    dmDescription += $"\n\n**Reason:** {reason}";
                }

                var dmEmbedBuilder = new EmbedBuilder()
                    .WithTitle($"🔨 Banned from {Context.Guild.Name}")
                    .WithDescription(dmDescription)
                    .AddField("Case Number", $"#{caseDto.CaseNumber}", inline: true)
                    .AddField("Moderator", Context.User.Username, inline: true)
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp();

                if (caseDto.ExpiresAt.HasValue)
                {
                    var expiresTimestamp = new DateTimeOffset(caseDto.ExpiresAt.Value).ToUnixTimeSeconds();
                    dmEmbedBuilder.AddField("Expires", $"<t:{expiresTimestamp}:F> (<t:{expiresTimestamp}:R>)", inline: false);
                }

                await user.SendMessageAsync(embed: dmEmbedBuilder.Build());
                _logger.LogDebug("Ban DM sent successfully to user {UserId}", user.Id);
            }
            catch (Exception dmEx)
            {
                _logger.LogWarning(dmEx, "Failed to send ban DM to user {UserId}", user.Id);
            }

            // Ban the user using Discord API
            await Context.Guild.AddBanAsync(user, deleteMessageDays, reason);
            _logger.LogInformation("User {UserId} banned from guild {GuildId}", user.Id, Context.Guild.Id);

            // Send confirmation embed
            var confirmEmbedBuilder = new EmbedBuilder()
                .WithTitle(parsedDuration.HasValue ? "🔨 User Temporarily Banned" : "🔨 User Permanently Banned")
                .WithColor(GetTypeColor(CaseType.Ban))
                .AddField("User", $"{user.Mention} ({user.Id})", inline: true)
                .AddField("Case", $"#{caseDto.CaseNumber}", inline: true)
                .AddField("Moderator", Context.User.Mention, inline: true)
                .WithCurrentTimestamp();

            if (!string.IsNullOrEmpty(reason))
            {
                confirmEmbedBuilder.AddField("Reason", reason);
            }

            if (parsedDuration.HasValue)
            {
                confirmEmbedBuilder.AddField("Duration", DurationParser.Format(parsedDuration.Value), inline: true);
            }

            if (caseDto.ExpiresAt.HasValue)
            {
                var expiresTimestamp = new DateTimeOffset(caseDto.ExpiresAt.Value).ToUnixTimeSeconds();
                confirmEmbedBuilder.AddField("Expires", $"<t:{expiresTimestamp}:F> (<t:{expiresTimestamp}:R>)", inline: false);
            }

            await RespondAsync(embed: confirmEmbedBuilder.Build());

            _logger.LogDebug("Ban command completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ban user {UserId}", user.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to ban user: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Timeout/mute a user.
    /// </summary>
    [SlashCommand("mute", "Timeout a user")]
    [RequireUserPermission(GuildPermission.ModerateMembers)]
    [RequireBotPermission(GuildPermission.ModerateMembers)]
    public async Task MuteAsync(
        [Summary("user", "The user to mute")] IGuildUser user,
        [Summary("duration", "Mute duration (e.g., '10m', '1h', '1d')")] string duration,
        [Summary("reason", "Reason for the mute")] string? reason = null)
    {
        _logger.LogInformation(
            "Mute command executed by {ModeratorUsername} (ID: {ModeratorId}) for user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId}), duration: {Duration}",
            Context.User.Username,
            Context.User.Id,
            user.Username,
            user.Id,
            Context.Guild.Name,
            Context.Guild.Id,
            duration);

        // Prevent self-mute
        if (user.Id == Context.User.Id)
        {
            await RespondAsync("You cannot mute yourself.", ephemeral: true);
            _logger.LogDebug("User {UserId} attempted to mute themselves", Context.User.Id);
            return;
        }

        // Prevent muting the bot
        if (user.IsBot && user.Id == Context.Client.CurrentUser.Id)
        {
            await RespondAsync("I cannot mute myself.", ephemeral: true);
            _logger.LogDebug("User {UserId} attempted to mute the bot", Context.User.Id);
            return;
        }

        // Check role hierarchy
        if (Context.User is SocketGuildUser moderator && user.Hierarchy >= moderator.Hierarchy)
        {
            await RespondAsync("You cannot mute a user with an equal or higher role than yours.", ephemeral: true);
            _logger.LogDebug(
                "User {ModeratorId} attempted to mute user {TargetId} with equal/higher role hierarchy",
                Context.User.Id,
                user.Id);
            return;
        }

        try
        {
            // Parse duration - required
            var parsedDuration = DurationParser.Parse(duration);
            if (!parsedDuration.HasValue)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Invalid Duration Format")
                    .WithDescription("Could not parse the duration you provided. Use formats like:\n• `10m` - 10 minutes\n• `1h` - 1 hour\n• `1h30m` - 1 hour 30 minutes\n• `1d` - 1 day")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                _logger.LogDebug("Failed to parse mute duration input: {DurationInput}", duration);
                return;
            }

            // Validate duration (Discord timeout max is 28 days)
            if (parsedDuration.Value.TotalDays > 28)
            {
                var errorEmbed = new EmbedBuilder()
                    .WithTitle("❌ Duration Too Long")
                    .WithDescription("Discord timeouts can only be applied for a maximum of 28 days.")
                    .WithColor(Color.Red)
                    .WithCurrentTimestamp()
                    .Build();

                await RespondAsync(embed: errorEmbed, ephemeral: true);
                _logger.LogDebug("Mute duration {Duration} exceeds 28 day limit", parsedDuration.Value);
                return;
            }

            _logger.LogDebug("Parsed mute duration: {Duration}", parsedDuration.Value);

            // Create moderation case
            var createDto = new ModerationCaseCreateDto
            {
                GuildId = Context.Guild.Id,
                TargetUserId = user.Id,
                ModeratorUserId = Context.User.Id,
                Type = CaseType.Mute,
                Reason = reason,
                Duration = parsedDuration.Value
            };

            var caseDto = await _moderationService.CreateCaseAsync(createDto);

            _logger.LogInformation(
                "Mute case created: Case #{CaseNumber} for user {TargetId} by moderator {ModeratorId}, expires: {ExpiresAt}",
                caseDto.CaseNumber,
                user.Id,
                Context.User.Id,
                caseDto.ExpiresAt);

            // Apply timeout using Discord API
            await user.SetTimeOutAsync(parsedDuration.Value, new RequestOptions { AuditLogReason = reason });
            _logger.LogInformation("User {UserId} muted in guild {GuildId} for {Duration}", user.Id, Context.Guild.Id, parsedDuration.Value);

            // Send confirmation embed
            var expiresAt = DateTime.UtcNow.Add(parsedDuration.Value);
            var expiresTimestamp = new DateTimeOffset(expiresAt).ToUnixTimeSeconds();

            var confirmEmbedBuilder = new EmbedBuilder()
                .WithTitle("🔇 User Muted")
                .WithColor(GetTypeColor(CaseType.Mute))
                .AddField("User", $"{user.Mention} ({user.Id})", inline: true)
                .AddField("Case", $"#{caseDto.CaseNumber}", inline: true)
                .AddField("Moderator", Context.User.Mention, inline: true)
                .WithCurrentTimestamp();

            if (!string.IsNullOrEmpty(reason))
            {
                confirmEmbedBuilder.AddField("Reason", reason);
            }

            confirmEmbedBuilder.AddField("Duration", DurationParser.Format(parsedDuration.Value), inline: true);
            confirmEmbedBuilder.AddField("Expires", $"<t:{expiresTimestamp}:F> (<t:{expiresTimestamp}:R>)", inline: false);

            await RespondAsync(embed: confirmEmbedBuilder.Build());

            _logger.LogDebug("Mute command completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mute user {UserId}", user.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to mute user: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
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

    /// <summary>
    /// Builds a confirmation embed for moderation actions.
    /// </summary>
    private Embed BuildActionEmbed(string title, IUser target, CaseType type, int caseNumber, string? reason, TimeSpan? duration = null)
    {
        var embed = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(GetTypeColor(type))
            .AddField("User", $"{target.Mention} ({target.Id})", inline: true)
            .AddField("Case", $"#{caseNumber}", inline: true)
            .AddField("Moderator", Context.User.Mention, inline: true)
            .WithCurrentTimestamp();

        if (!string.IsNullOrEmpty(reason))
        {
            embed.AddField("Reason", reason);
        }

        if (duration.HasValue)
        {
            embed.AddField("Duration", DurationParser.Format(duration.Value), inline: true);
        }

        return embed.Build();
    }

    /// <summary>
    /// Gets the embed color for a case type.
    /// </summary>
    private Color GetTypeColor(CaseType type) => type switch
    {
        CaseType.Warn => Color.Gold,
        CaseType.Kick => Color.Orange,
        CaseType.Ban => Color.Red,
        CaseType.Mute => Color.LightOrange,
        _ => Color.Blue
    };
}
