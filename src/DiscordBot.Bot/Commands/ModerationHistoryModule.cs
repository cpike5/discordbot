using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Bot.Utilities;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using System.Text;
using System.Text.Json;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Slash commands for viewing and managing moderation case history.
/// </summary>
[RequireGuildActive]
[RequireModerationEnabled]
[RequireModerator]
public class ModerationHistoryModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IModerationService _moderationService;
    private readonly ILogger<ModerationHistoryModule> _logger;
    private const int PageSize = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModerationHistoryModule"/> class.
    /// </summary>
    public ModerationHistoryModule(
        IModerationService moderationService,
        ILogger<ModerationHistoryModule> logger)
    {
        _moderationService = moderationService;
        _logger = logger;
    }

    /// <summary>
    /// View a user's moderation history.
    /// </summary>
    [SlashCommand("modlog", "View a user's moderation history")]
    public async Task ModLogAsync(
        [Summary("user", "The user to check")] IUser user,
        [Summary("page", "Page number")] int page = 1)
    {
        _logger.LogInformation(
            "Modlog command executed by {ModeratorUsername} (ID: {ModeratorId}) for user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId}), page: {Page}",
            Context.User.Username,
            Context.User.Id,
            user.Username,
            user.Id,
            Context.Guild.Name,
            Context.Guild.Id,
            page);

        // Validate page number
        if (page < 1)
        {
            await RespondAsync("Page number must be at least 1.", ephemeral: true);
            return;
        }

        try
        {
            // Get user cases paginated
            var (cases, totalCount) = await _moderationService.GetUserCasesAsync(
                Context.Guild.Id,
                user.Id,
                page,
                PageSize);

            var caseList = cases.ToList();
            var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

            _logger.LogDebug(
                "Retrieved {CaseCount} cases for user {UserId}, page {Page} of {TotalPages}",
                caseList.Count,
                user.Id,
                page,
                totalPages);

            // Build embed
            var embed = new EmbedBuilder()
                .WithTitle($"📋 Moderation History: {user.Username}")
                .WithColor(totalCount == 0 ? Color.Green : Color.Orange)
                .WithThumbnailUrl(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithFooter($"Page {page} of {Math.Max(totalPages, 1)} • Total Cases: {totalCount}")
                .WithCurrentTimestamp();

            if (caseList.Count == 0)
            {
                embed.WithDescription("This user has a clean record!");
            }
            else
            {
                var description = new StringBuilder();

                foreach (var caseDto in caseList)
                {
                    var typeEmoji = GetTypeEmoji(caseDto.Type);
                    var timestamp = new DateTimeOffset(caseDto.CreatedAt).ToUnixTimeSeconds();
                    var reasonText = string.IsNullOrWhiteSpace(caseDto.Reason)
                        ? "*No reason provided*"
                        : caseDto.Reason;

                    // Truncate long reasons
                    if (reasonText.Length > 100)
                    {
                        reasonText = reasonText[..97] + "...";
                    }

                    description.AppendLine($"{typeEmoji} **Case #{caseDto.CaseNumber}** — {caseDto.Type}");
                    description.AppendLine($"<t:{timestamp}:D> (<t:{timestamp}:R>)");
                    description.AppendLine($"> {reasonText}");
                    description.AppendLine($"*Moderator: {caseDto.ModeratorUsername}*");
                    description.AppendLine();
                }

                embed.WithDescription(description.ToString());
            }

            // Add pagination buttons if multiple pages
            var components = new ComponentBuilder();

            if (totalPages > 1)
            {
                var correlationId = Guid.NewGuid().ToString("N");

                // Previous button
                var prevButtonId = ComponentIdBuilder.Build(
                    "modlog",
                    "page",
                    Context.User.Id,
                    correlationId,
                    $"{user.Id}:{Math.Max(1, page - 1)}");

                components.WithButton(
                    "◀ Previous",
                    prevButtonId,
                    ButtonStyle.Secondary,
                    disabled: page <= 1);

                // Next button
                var nextButtonId = ComponentIdBuilder.Build(
                    "modlog",
                    "page",
                    Context.User.Id,
                    correlationId,
                    $"{user.Id}:{Math.Min(totalPages, page + 1)}");

                components.WithButton(
                    "Next ▶",
                    nextButtonId,
                    ButtonStyle.Secondary,
                    disabled: page >= totalPages);
            }

            await RespondAsync(
                embed: embed.Build(),
                components: totalPages > 1 ? components.Build() : null);

            _logger.LogDebug("Modlog command completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve moderation history for user {UserId}", user.Id);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to retrieve moderation history: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// View details of a specific case.
    /// </summary>
    [SlashCommand("case", "View details of a specific case")]
    public async Task CaseAsync(
        [Summary("id", "The case number")] int caseNumber)
    {
        _logger.LogInformation(
            "Case command executed by {ModeratorUsername} (ID: {ModeratorId}) for case #{CaseNumber} in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            caseNumber,
            Context.Guild.Name,
            Context.Guild.Id);

        try
        {
            // Get case by guild + number
            var caseDto = await _moderationService.GetCaseByNumberAsync(Context.Guild.Id, caseNumber);

            if (caseDto == null)
            {
                await RespondAsync($"Case #{caseNumber} not found.", ephemeral: true);
                _logger.LogDebug("Case #{CaseNumber} not found in guild {GuildId}", caseNumber, Context.Guild.Id);
                return;
            }

            _logger.LogDebug("Retrieved case #{CaseNumber} details", caseNumber);

            // Build detailed embed
            var typeEmoji = GetTypeEmoji(caseDto.Type);
            var timestamp = new DateTimeOffset(caseDto.CreatedAt).ToUnixTimeSeconds();

            var embed = new EmbedBuilder()
                .WithTitle($"{typeEmoji} Case #{caseDto.CaseNumber} — {caseDto.Type}")
                .WithColor(GetTypeColor(caseDto.Type))
                .AddField("User", $"<@{caseDto.TargetUserId}> ({caseDto.TargetUsername})\n`{caseDto.TargetUserId}`", inline: true)
                .AddField("Moderator", $"<@{caseDto.ModeratorUserId}> ({caseDto.ModeratorUsername})\n`{caseDto.ModeratorUserId}`", inline: true)
                .AddField("Date", $"<t:{timestamp}:F>\n<t:{timestamp}:R>", inline: true)
                .WithCurrentTimestamp();

            if (!string.IsNullOrWhiteSpace(caseDto.Reason))
            {
                embed.AddField("Reason", caseDto.Reason);
            }
            else
            {
                embed.AddField("Reason", "*No reason provided*");
            }

            if (caseDto.Duration.HasValue)
            {
                embed.AddField("Duration", DurationParser.Format(caseDto.Duration.Value), inline: true);
            }

            if (caseDto.ExpiresAt.HasValue)
            {
                var expiresTimestamp = new DateTimeOffset(caseDto.ExpiresAt.Value).ToUnixTimeSeconds();
                var isExpired = caseDto.ExpiresAt.Value < DateTime.UtcNow;

                embed.AddField(
                    isExpired ? "Expired" : "Expires",
                    $"<t:{expiresTimestamp}:F>\n<t:{expiresTimestamp}:R>",
                    inline: true);
            }

            await RespondAsync(embed: embed.Build());

            _logger.LogDebug("Case command completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve case #{CaseNumber}", caseNumber);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to retrieve case details: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Update the reason for a case.
    /// </summary>
    [SlashCommand("reason", "Update the reason for a case")]
    public async Task ReasonAsync(
        [Summary("case_id", "The case number")] int caseNumber,
        [Summary("reason", "The new reason")] string reason)
    {
        _logger.LogInformation(
            "Reason update command executed by {ModeratorUsername} (ID: {ModeratorId}) for case #{CaseNumber} in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            caseNumber,
            Context.Guild.Name,
            Context.Guild.Id);

        // Validate reason is not empty
        if (string.IsNullOrWhiteSpace(reason))
        {
            await RespondAsync("Reason cannot be empty.", ephemeral: true);
            return;
        }

        try
        {
            // Update case reason
            var updatedCase = await _moderationService.UpdateCaseReasonAsync(
                Context.Guild.Id,
                caseNumber,
                reason,
                Context.User.Id);

            if (updatedCase == null)
            {
                await RespondAsync($"Case #{caseNumber} not found.", ephemeral: true);
                _logger.LogDebug("Case #{CaseNumber} not found in guild {GuildId}", caseNumber, Context.Guild.Id);
                return;
            }

            _logger.LogInformation(
                "Case #{CaseNumber} reason updated by moderator {ModeratorId}",
                caseNumber,
                Context.User.Id);

            // Send confirmation embed
            var typeEmoji = GetTypeEmoji(updatedCase.Type);

            var embed = new EmbedBuilder()
                .WithTitle($"✅ Case Reason Updated")
                .WithDescription($"{typeEmoji} **Case #{updatedCase.CaseNumber}** — {updatedCase.Type}")
                .AddField("User", $"<@{updatedCase.TargetUserId}>", inline: true)
                .AddField("Updated By", Context.User.Mention, inline: true)
                .AddField("New Reason", reason)
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: embed);

            _logger.LogDebug("Reason update command completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update reason for case #{CaseNumber}", caseNumber);

            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Error")
                .WithDescription($"Failed to update case reason: {ex.Message}")
                .WithColor(Color.Red)
                .WithCurrentTimestamp()
                .Build();

            await RespondAsync(embed: errorEmbed, ephemeral: true);
        }
    }

    /// <summary>
    /// Export a user's moderation history to a file.
    /// </summary>
    [SlashCommand("modexport", "Export a user's moderation history to a file")]
    public async Task ExportAsync(
        [Summary("user", "The user to export history for")] IUser user)
    {
        _logger.LogInformation(
            "Modexport command executed by {ModeratorUsername} (ID: {ModeratorId}) for user {TargetUsername} (ID: {TargetId}) in guild {GuildName} (ID: {GuildId})",
            Context.User.Username,
            Context.User.Id,
            user.Username,
            user.Id,
            Context.Guild.Name,
            Context.Guild.Id);

        await DeferAsync(ephemeral: true);

        try
        {
            // Export history using service
            var fileBytes = await _moderationService.ExportUserHistoryAsync(Context.Guild.Id, user.Id);

            if (fileBytes.Length == 0)
            {
                await FollowupAsync("No moderation history found for this user.", ephemeral: true);
                _logger.LogDebug("No moderation history found for user {UserId}", user.Id);
                return;
            }

            _logger.LogInformation(
                "Exported {ByteCount} bytes of moderation history for user {UserId}",
                fileBytes.Length,
                user.Id);

            // Upload as file attachment
            var fileName = $"moderation_history_{user.Username}_{user.Id}_{DateTime.UtcNow:yyyyMMdd}.json";
            using var stream = new MemoryStream(fileBytes);

            await FollowupWithFileAsync(
                stream,
                fileName,
                text: $"📄 Moderation history for **{user.Username}** exported successfully.",
                ephemeral: true);

            _logger.LogDebug("Modexport command completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export moderation history for user {UserId}", user.Id);

            await FollowupAsync($"Failed to export moderation history: {ex.Message}", ephemeral: true);
        }
    }

    /// <summary>
    /// Gets the emoji representation for a case type.
    /// </summary>
    private static string GetTypeEmoji(CaseType type) => type switch
    {
        CaseType.Warn => "⚠️",
        CaseType.Kick => "🥾",
        CaseType.Ban => "🔨",
        CaseType.Mute => "🔇",
        CaseType.Note => "📝",
        _ => "❓"
    };

    /// <summary>
    /// Gets the embed color for a case type.
    /// </summary>
    private static Color GetTypeColor(CaseType type) => type switch
    {
        CaseType.Warn => Color.Gold,
        CaseType.Kick => Color.Orange,
        CaseType.Ban => Color.Red,
        CaseType.Mute => Color.LightOrange,
        CaseType.Note => Color.Blue,
        _ => Color.Default
    };
}
