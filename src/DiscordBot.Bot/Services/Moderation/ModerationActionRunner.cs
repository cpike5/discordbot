using Discord;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Utilities;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Moderation;

/// <summary>
/// The outcome of running a moderation action through <see cref="IModerationActionRunner"/>.
/// Exactly one of <see cref="Embed"/> or <see cref="PlainText"/> is set.
/// </summary>
public record ModerationActionResult(bool Success, Embed? Embed, string? PlainText, bool Ephemeral)
{
    public static ModerationActionResult PlainError(string message) => new(false, null, message, true);

    public static ModerationActionResult EmbedError(Embed embed) => new(false, embed, null, true);

    public static ModerationActionResult Ok(Embed embed, bool ephemeral = false) => new(true, embed, null, ephemeral);
}

/// <summary>
/// Everything the shared moderation pipeline needs from the invoking slash-command interaction,
/// abstracted away from Discord.Net's concrete (largely non-mockable) socket types so
/// <see cref="ModerationActionRunner"/> can be unit tested. The Bot layer adapts a real
/// <c>SocketInteractionContext</c> to this interface (see <c>InteractionModerationCommandContext</c>).
/// </summary>
public interface IModerationCommandContext
{
    /// <summary>The guild the command was invoked in.</summary>
    IGuild Guild { get; }

    /// <summary>The moderator who invoked the command.</summary>
    IUser ModeratorUser { get; }

    /// <summary>The bot's own user id, used to block self-moderation of the bot.</summary>
    ulong BotUserId { get; }

    /// <summary>The moderator's role hierarchy position, or null when they are not resolvable as a guild member.</summary>
    int? ModeratorHierarchy { get; }

    /// <summary>Resolves the target as a guild member, checking cache first and falling back to a REST lookup.</summary>
    Task<IGuildUser?> ResolveGuildUserAsync(IUser user);
}

/// <summary>
/// Runs the shared validate &#8594; perform Discord action &#8594; DM notify &#8594; create case &#8594; reply pipeline
/// that backs the warn/kick/ban/unban/mute slash commands, so <see cref="Commands.ModerationActionModule"/>
/// only has to build a request and hand it off.
/// </summary>
public interface IModerationActionRunner
{
    Task<ModerationActionResult> WarnAsync(IModerationCommandContext context, IUser user, string? reason);

    Task<ModerationActionResult> KickAsync(IModerationCommandContext context, IUser user, string? reason);

    Task<ModerationActionResult> BanAsync(IModerationCommandContext context, IUser user, string? duration, string? reason, int deleteMessageDays);

    Task<ModerationActionResult> UnbanAsync(IModerationCommandContext context, string userId, string? reason);

    Task<ModerationActionResult> MuteAsync(IModerationCommandContext context, IUser user, string duration, string? reason);
}

/// <inheritdoc cref="IModerationActionRunner"/>
public class ModerationActionRunner : IModerationActionRunner
{
    private readonly IModerationService _moderationService;
    private readonly ILogger<ModerationActionRunner> _logger;

    public ModerationActionRunner(IModerationService moderationService, ILogger<ModerationActionRunner> logger)
    {
        _moderationService = moderationService;
        _logger = logger;
    }

    public async Task<ModerationActionResult> WarnAsync(IModerationCommandContext context, IUser user, string? reason)
    {
        _logger.LogInformation(
            "Warn command executed by {ModeratorUsername} (ID: {ModeratorId}) for user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            context.ModeratorUser.Username, context.ModeratorUser.Id, user.Username, user.Id, context.Guild.Name, context.Guild.Id);

        if (user.Id == context.ModeratorUser.Id)
        {
            _logger.LogDebug("User {UserId} attempted to warn themselves", context.ModeratorUser.Id);
            return ModerationActionResult.PlainError("You cannot warn yourself.");
        }

        if (user.IsBot && user.Id == context.BotUserId)
        {
            _logger.LogDebug("User {UserId} attempted to warn the bot", context.ModeratorUser.Id);
            return ModerationActionResult.PlainError("I cannot be warned.");
        }

        try
        {
            var createDto = new ModerationCaseCreateDto
            {
                GuildId = context.Guild.Id,
                TargetUserId = user.Id,
                ModeratorUserId = context.ModeratorUser.Id,
                Type = CaseType.Warn,
                Reason = reason
            };

            var caseDto = await _moderationService.CreateCaseAsync(createDto);

            _logger.LogInformation(
                "Warning issued: Case #{CaseNumber} for user {TargetId} by moderator {ModeratorId}",
                caseDto.CaseNumber, user.Id, context.ModeratorUser.Id);

            try
            {
                var dmEmbed = new EmbedBuilder()
                    .WithTitle($"⚠️ Warning in {context.Guild.Name}")
                    .WithDescription(string.IsNullOrWhiteSpace(reason)
                        ? "You have received a formal warning."
                        : $"**Reason:** {reason}")
                    .AddField("Case Number", $"#{caseDto.CaseNumber}", inline: true)
                    .AddField("Moderator", context.ModeratorUser.Username, inline: true)
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

            var confirmEmbed = BuildActionEmbed(context, "⚠️ Warning Issued", user, CaseType.Warn, caseDto.CaseNumber, reason);

            _logger.LogDebug("Warn command completed successfully");
            return ModerationActionResult.Ok(confirmEmbed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warn user {UserId}", user.Id);
            return ModerationActionResult.EmbedError(EmbedHelper.Error("Error", $"Failed to issue warning: {ex.Message}"));
        }
    }

    public async Task<ModerationActionResult> KickAsync(IModerationCommandContext context, IUser user, string? reason)
    {
        _logger.LogInformation(
            "Kick command executed by {ModeratorUsername} (ID: {ModeratorId}) for user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            context.ModeratorUser.Username, context.ModeratorUser.Id, user.Username, user.Id, context.Guild.Name, context.Guild.Id);

        var guildUser = await context.ResolveGuildUserAsync(user);
        if (guildUser == null)
        {
            return ModerationActionResult.PlainError("Could not find that user in this server. They may have left or were never a member.");
        }

        if (guildUser.Id == context.ModeratorUser.Id)
        {
            _logger.LogDebug("User {UserId} attempted to kick themselves", context.ModeratorUser.Id);
            return ModerationActionResult.PlainError("You cannot kick yourself.");
        }

        if (guildUser.IsBot && guildUser.Id == context.BotUserId)
        {
            _logger.LogDebug("User {UserId} attempted to kick the bot", context.ModeratorUser.Id);
            return ModerationActionResult.PlainError("I cannot kick myself.");
        }

        if (context.ModeratorHierarchy.HasValue && guildUser.Hierarchy >= context.ModeratorHierarchy.Value)
        {
            _logger.LogDebug(
                "User {ModeratorId} attempted to kick user {TargetId} with equal/higher role hierarchy",
                context.ModeratorUser.Id, guildUser.Id);
            return ModerationActionResult.PlainError("You cannot kick a user with an equal or higher role than yours.");
        }

        try
        {
            var createDto = new ModerationCaseCreateDto
            {
                GuildId = context.Guild.Id,
                TargetUserId = guildUser.Id,
                ModeratorUserId = context.ModeratorUser.Id,
                Type = CaseType.Kick,
                Reason = reason
            };

            var caseDto = await _moderationService.CreateCaseAsync(createDto);

            _logger.LogInformation(
                "Kick case created: Case #{CaseNumber} for user {TargetId} by moderator {ModeratorId}",
                caseDto.CaseNumber, guildUser.Id, context.ModeratorUser.Id);

            try
            {
                var dmEmbed = new EmbedBuilder()
                    .WithTitle($"🥾 Kicked from {context.Guild.Name}")
                    .WithDescription(string.IsNullOrWhiteSpace(reason)
                        ? "You have been kicked from the server."
                        : $"**Reason:** {reason}")
                    .AddField("Case Number", $"#{caseDto.CaseNumber}", inline: true)
                    .AddField("Moderator", context.ModeratorUser.Username, inline: true)
                    .WithColor(Color.Orange)
                    .WithCurrentTimestamp()
                    .Build();

                await guildUser.SendMessageAsync(embed: dmEmbed);
                _logger.LogDebug("Kick DM sent successfully to user {UserId}", guildUser.Id);
            }
            catch (Exception dmEx)
            {
                _logger.LogWarning(dmEx, "Failed to send kick DM to user {UserId}", guildUser.Id);
            }

            await guildUser.KickAsync(reason);
            _logger.LogInformation("User {UserId} kicked from guild {GuildId}", guildUser.Id, context.Guild.Id);

            var confirmEmbed = BuildActionEmbed(context, "🥾 User Kicked", guildUser, CaseType.Kick, caseDto.CaseNumber, reason);

            _logger.LogDebug("Kick command completed successfully");
            return ModerationActionResult.Ok(confirmEmbed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to kick user {UserId}", guildUser.Id);
            return ModerationActionResult.EmbedError(EmbedHelper.Error("Error", $"Failed to kick user: {ex.Message}"));
        }
    }

    public async Task<ModerationActionResult> BanAsync(IModerationCommandContext context, IUser user, string? duration, string? reason, int deleteMessageDays)
    {
        _logger.LogInformation(
            "Ban command executed by {ModeratorUsername} (ID: {ModeratorId}) for user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId}), duration: {Duration}",
            context.ModeratorUser.Username, context.ModeratorUser.Id, user.Username, user.Id, context.Guild.Name, context.Guild.Id, duration ?? "permanent");

        if (user.Id == context.ModeratorUser.Id)
        {
            _logger.LogDebug("User {UserId} attempted to ban themselves", context.ModeratorUser.Id);
            return ModerationActionResult.PlainError("You cannot ban yourself.");
        }

        if (user.IsBot && user.Id == context.BotUserId)
        {
            _logger.LogDebug("User {UserId} attempted to ban the bot", context.ModeratorUser.Id);
            return ModerationActionResult.PlainError("I cannot ban myself.");
        }

        if (user is IGuildUser guildUserForHierarchy && context.ModeratorHierarchy.HasValue)
        {
            if (guildUserForHierarchy.Hierarchy >= context.ModeratorHierarchy.Value)
            {
                _logger.LogDebug(
                    "User {ModeratorId} attempted to ban user {TargetId} with equal/higher role hierarchy",
                    context.ModeratorUser.Id, user.Id);
                return ModerationActionResult.PlainError("You cannot ban a user with an equal or higher role than yours.");
            }
        }

        try
        {
            TimeSpan? parsedDuration = null;
            if (!string.IsNullOrWhiteSpace(duration))
            {
                parsedDuration = DurationParser.Parse(duration);
                if (!parsedDuration.HasValue)
                {
                    _logger.LogDebug("Failed to parse ban duration input: {DurationInput}", duration);
                    return ModerationActionResult.EmbedError(EmbedHelper.Error(
                        "Invalid Duration Format",
                        "Could not parse the duration you provided. Use formats like:\n• `7d` - 7 days\n• `24h` - 24 hours\n• `1h30m` - 1 hour 30 minutes"));
                }

                _logger.LogDebug("Parsed ban duration: {Duration}", parsedDuration.Value);
            }

            var createDto = new ModerationCaseCreateDto
            {
                GuildId = context.Guild.Id,
                TargetUserId = user.Id,
                ModeratorUserId = context.ModeratorUser.Id,
                Type = CaseType.Ban,
                Reason = reason,
                Duration = parsedDuration
            };

            var caseDto = await _moderationService.CreateCaseAsync(createDto);

            _logger.LogInformation(
                "Ban case created: Case #{CaseNumber} for user {TargetId} by moderator {ModeratorId}, expires: {ExpiresAt}",
                caseDto.CaseNumber, user.Id, context.ModeratorUser.Id, caseDto.ExpiresAt?.ToString() ?? "never");

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
                    .WithTitle($"🔨 Banned from {context.Guild.Name}")
                    .WithDescription(dmDescription)
                    .AddField("Case Number", $"#{caseDto.CaseNumber}", inline: true)
                    .AddField("Moderator", context.ModeratorUser.Username, inline: true)
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

            await context.Guild.AddBanAsync(user, deleteMessageDays, reason);
            _logger.LogInformation("User {UserId} banned from guild {GuildId}", user.Id, context.Guild.Id);

            var confirmEmbedBuilder = new EmbedBuilder()
                .WithTitle(parsedDuration.HasValue ? "🔨 User Temporarily Banned" : "🔨 User Permanently Banned")
                .WithColor(GetTypeColor(CaseType.Ban))
                .AddField("User", $"{user.Mention} ({user.Id})", inline: true)
                .AddField("Case", $"#{caseDto.CaseNumber}", inline: true)
                .AddField("Moderator", context.ModeratorUser.Mention, inline: true)
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

            _logger.LogDebug("Ban command completed successfully");
            return ModerationActionResult.Ok(confirmEmbedBuilder.Build());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ban user {UserId}", user.Id);
            return ModerationActionResult.EmbedError(EmbedHelper.Error("Error", $"Failed to ban user: {ex.Message}"));
        }
    }

    public async Task<ModerationActionResult> UnbanAsync(IModerationCommandContext context, string userId, string? reason)
    {
        _logger.LogInformation(
            "Unban command executed by {ModeratorUsername} (ID: {ModeratorId}) for user ID {TargetId} in guild {GuildName} (ID: {GuildId})",
            context.ModeratorUser.Username, context.ModeratorUser.Id, userId, context.Guild.Name, context.Guild.Id);

        if (!ulong.TryParse(userId, out var targetUserId))
        {
            _logger.LogDebug("Invalid user ID format provided: {UserId}", userId);
            return ModerationActionResult.PlainError("Invalid user ID format. Please provide a valid Discord user ID.");
        }

        try
        {
            var ban = await context.Guild.GetBanAsync(targetUserId);
            if (ban == null)
            {
                _logger.LogDebug("User {UserId} is not banned in guild {GuildId}", targetUserId, context.Guild.Id);
                return ModerationActionResult.PlainError("That user is not banned from this server.");
            }

            var createDto = new ModerationCaseCreateDto
            {
                GuildId = context.Guild.Id,
                TargetUserId = targetUserId,
                ModeratorUserId = context.ModeratorUser.Id,
                Type = CaseType.Unban,
                Reason = reason
            };

            var caseDto = await _moderationService.CreateCaseAsync(createDto);

            _logger.LogInformation(
                "Unban case created: Case #{CaseNumber} for user {TargetId} by moderator {ModeratorId}",
                caseDto.CaseNumber, targetUserId, context.ModeratorUser.Id);

            await context.Guild.RemoveBanAsync(targetUserId, new RequestOptions { AuditLogReason = reason });
            _logger.LogInformation("User {UserId} unbanned from guild {GuildId}", targetUserId, context.Guild.Id);

            var confirmEmbedBuilder = new EmbedBuilder()
                .WithTitle("✅ User Unbanned")
                .WithColor(GetTypeColor(CaseType.Unban))
                .AddField("User", $"{ban.User.Username} ({ban.User.Id})", inline: true)
                .AddField("Case", $"#{caseDto.CaseNumber}", inline: true)
                .AddField("Moderator", context.ModeratorUser.Mention, inline: true)
                .WithCurrentTimestamp();

            if (!string.IsNullOrEmpty(reason))
            {
                confirmEmbedBuilder.AddField("Reason", reason);
            }

            if (!string.IsNullOrEmpty(ban.Reason))
            {
                confirmEmbedBuilder.AddField("Original Ban Reason", ban.Reason);
            }

            _logger.LogDebug("Unban command completed successfully");
            return ModerationActionResult.Ok(confirmEmbedBuilder.Build());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unban user {UserId}", targetUserId);
            return ModerationActionResult.EmbedError(EmbedHelper.Error("Error", $"Failed to unban user: {ex.Message}"));
        }
    }

    public async Task<ModerationActionResult> MuteAsync(IModerationCommandContext context, IUser user, string duration, string? reason)
    {
        _logger.LogInformation(
            "Mute command executed by {ModeratorUsername} (ID: {ModeratorId}) for user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId}), duration: {Duration}",
            context.ModeratorUser.Username, context.ModeratorUser.Id, user.Username, user.Id, context.Guild.Name, context.Guild.Id, duration);

        var guildUser = await context.ResolveGuildUserAsync(user);
        if (guildUser == null)
        {
            return ModerationActionResult.PlainError("Could not find that user in this server. They may have left or were never a member.");
        }

        if (guildUser.Id == context.ModeratorUser.Id)
        {
            _logger.LogDebug("User {UserId} attempted to mute themselves", context.ModeratorUser.Id);
            return ModerationActionResult.PlainError("You cannot mute yourself.");
        }

        if (guildUser.IsBot && guildUser.Id == context.BotUserId)
        {
            _logger.LogDebug("User {UserId} attempted to mute the bot", context.ModeratorUser.Id);
            return ModerationActionResult.PlainError("I cannot mute myself.");
        }

        if (context.ModeratorHierarchy.HasValue && guildUser.Hierarchy >= context.ModeratorHierarchy.Value)
        {
            _logger.LogDebug(
                "User {ModeratorId} attempted to mute user {TargetId} with equal/higher role hierarchy",
                context.ModeratorUser.Id, guildUser.Id);
            return ModerationActionResult.PlainError("You cannot mute a user with an equal or higher role than yours.");
        }

        try
        {
            var parsedDuration = DurationParser.Parse(duration);
            if (!parsedDuration.HasValue)
            {
                _logger.LogDebug("Failed to parse mute duration input: {DurationInput}", duration);
                return ModerationActionResult.EmbedError(EmbedHelper.Error(
                    "Invalid Duration Format",
                    "Could not parse the duration you provided. Use formats like:\n• `10m` - 10 minutes\n• `1h` - 1 hour\n• `1h30m` - 1 hour 30 minutes\n• `1d` - 1 day"));
            }

            if (parsedDuration.Value.TotalDays > 28)
            {
                _logger.LogDebug("Mute duration {Duration} exceeds 28 day limit", parsedDuration.Value);
                return ModerationActionResult.EmbedError(EmbedHelper.Error(
                    "Duration Too Long",
                    "Discord timeouts can only be applied for a maximum of 28 days."));
            }

            _logger.LogDebug("Parsed mute duration: {Duration}", parsedDuration.Value);

            var createDto = new ModerationCaseCreateDto
            {
                GuildId = context.Guild.Id,
                TargetUserId = guildUser.Id,
                ModeratorUserId = context.ModeratorUser.Id,
                Type = CaseType.Mute,
                Reason = reason,
                Duration = parsedDuration.Value
            };

            var caseDto = await _moderationService.CreateCaseAsync(createDto);

            _logger.LogInformation(
                "Mute case created: Case #{CaseNumber} for user {TargetId} by moderator {ModeratorId}, expires: {ExpiresAt}",
                caseDto.CaseNumber, guildUser.Id, context.ModeratorUser.Id, caseDto.ExpiresAt);

            await guildUser.SetTimeOutAsync(parsedDuration.Value, new RequestOptions { AuditLogReason = reason });
            _logger.LogInformation("User {UserId} muted in guild {GuildId} for {Duration}", guildUser.Id, context.Guild.Id, parsedDuration.Value);

            var expiresAt = DateTime.UtcNow.Add(parsedDuration.Value);
            var expiresTimestamp = new DateTimeOffset(expiresAt).ToUnixTimeSeconds();

            var confirmEmbedBuilder = new EmbedBuilder()
                .WithTitle("🔇 User Muted")
                .WithColor(GetTypeColor(CaseType.Mute))
                .AddField("User", $"{guildUser.Mention} ({guildUser.Id})", inline: true)
                .AddField("Case", $"#{caseDto.CaseNumber}", inline: true)
                .AddField("Moderator", context.ModeratorUser.Mention, inline: true)
                .WithCurrentTimestamp();

            if (!string.IsNullOrEmpty(reason))
            {
                confirmEmbedBuilder.AddField("Reason", reason);
            }

            confirmEmbedBuilder.AddField("Duration", DurationParser.Format(parsedDuration.Value), inline: true);
            confirmEmbedBuilder.AddField("Expires", $"<t:{expiresTimestamp}:F> (<t:{expiresTimestamp}:R>)", inline: false);

            _logger.LogDebug("Mute command completed successfully");
            return ModerationActionResult.Ok(confirmEmbedBuilder.Build());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mute user {UserId}", guildUser.Id);
            return ModerationActionResult.EmbedError(EmbedHelper.Error("Error", $"Failed to mute user: {ex.Message}"));
        }
    }

    /// <summary>
    /// Builds a confirmation embed for moderation actions.
    /// </summary>
    private static Embed BuildActionEmbed(IModerationCommandContext context, string title, IUser target, CaseType type, int caseNumber, string? reason, TimeSpan? duration = null)
    {
        var embed = new EmbedBuilder()
            .WithTitle(title)
            .WithColor(GetTypeColor(type))
            .AddField("User", $"{target.Mention} ({target.Id})", inline: true)
            .AddField("Case", $"#{caseNumber}", inline: true)
            .AddField("Moderator", context.ModeratorUser.Mention, inline: true)
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
    private static Color GetTypeColor(CaseType type) => type switch
    {
        CaseType.Warn => Color.Gold,
        CaseType.Kick => Color.Orange,
        CaseType.Ban => Color.Red,
        CaseType.Mute => Color.LightOrange,
        CaseType.Unban => Color.Green,
        _ => Color.Blue
    };
}
