using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Components;
using DiscordBot.Bot.Utilities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using System.Text;

namespace DiscordBot.Bot.Commands;

/// <summary>
/// Component interaction handlers for moderation history pagination.
/// </summary>
public class ModerationHistoryComponentModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IModerationService _moderationService;
    private readonly ILogger<ModerationHistoryComponentModule> _logger;
    private const int PageSize = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModerationHistoryComponentModule"/> class.
    /// </summary>
    public ModerationHistoryComponentModule(
        IModerationService moderationService,
        ILogger<ModerationHistoryComponentModule> logger)
    {
        _moderationService = moderationService;
        _logger = logger;
    }

    /// <summary>
    /// Handles modlog pagination button interactions.
    /// Custom ID format: modlog:page:{userId}:{correlationId}:{targetUserId}:{page}
    /// </summary>
    [ComponentInteraction("modlog:page:*:*:*")]
    public async Task HandleModLogPageAsync(string userIdStr, string correlationId, string data)
    {
        _logger.LogDebug(
            "Modlog page navigation triggered by user {UserId}, correlationId: {CorrelationId}, data: {Data}",
            Context.User.Id,
            correlationId,
            data);

        // Parse user ID to verify authorization
        if (!ulong.TryParse(userIdStr, out var authorizedUserId))
        {
            await RespondAsync("Invalid interaction data.", ephemeral: true);
            _logger.LogWarning("Invalid user ID format in component interaction: {UserId}", userIdStr);
            return;
        }

        // Verify the user clicking the button is the one who invoked the command
        if (Context.User.Id != authorizedUserId)
        {
            await RespondAsync("Only the user who invoked this command can navigate pages.", ephemeral: true);
            _logger.LogDebug(
                "Unauthorized page navigation attempt by user {ActualUserId}, expected {AuthorizedUserId}",
                Context.User.Id,
                authorizedUserId);
            return;
        }

        // Parse data: {targetUserId}:{page}
        var dataParts = data.Split(':');
        if (dataParts.Length != 2)
        {
            await RespondAsync("Invalid page data.", ephemeral: true);
            _logger.LogWarning("Invalid data format in modlog page component: {Data}", data);
            return;
        }

        if (!ulong.TryParse(dataParts[0], out var targetUserId) || !int.TryParse(dataParts[1], out var page))
        {
            await RespondAsync("Invalid page data.", ephemeral: true);
            _logger.LogWarning("Failed to parse target user ID or page number: {Data}", data);
            return;
        }

        _logger.LogInformation(
            "Modlog page navigation: user {UserId} viewing history for {TargetUserId}, page {Page}",
            Context.User.Id,
            targetUserId,
            page);

        try
        {
            await DeferAsync();

            // Get target user info
            var targetUser = await Context.Client.GetUserAsync(targetUserId);
            if (targetUser == null)
            {
                await FollowupAsync("Unable to retrieve user information.", ephemeral: true);
                _logger.LogWarning("Failed to retrieve user {TargetUserId} from Discord", targetUserId);
                return;
            }

            // Get user cases paginated
            var (cases, totalCount) = await _moderationService.GetUserCasesAsync(
                Context.Guild.Id,
                targetUserId,
                page,
                PageSize);

            var caseList = cases.ToList();
            var totalPages = (int)Math.Ceiling(totalCount / (double)PageSize);

            _logger.LogDebug(
                "Retrieved {CaseCount} cases for user {UserId}, page {Page} of {TotalPages}",
                caseList.Count,
                targetUserId,
                page,
                totalPages);

            // Build embed
            var embed = new EmbedBuilder()
                .WithTitle($"📋 Moderation History: {targetUser.Username}")
                .WithColor(totalCount == 0 ? Color.Green : Color.Orange)
                .WithThumbnailUrl(targetUser.GetAvatarUrl() ?? targetUser.GetDefaultAvatarUrl())
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
                // Previous button
                var prevButtonId = ComponentIdBuilder.Build(
                    "modlog",
                    "page",
                    Context.User.Id,
                    correlationId,
                    $"{targetUserId}:{Math.Max(1, page - 1)}");

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
                    $"{targetUserId}:{Math.Min(totalPages, page + 1)}");

                components.WithButton(
                    "Next ▶",
                    nextButtonId,
                    ButtonStyle.Secondary,
                    disabled: page >= totalPages);
            }

            // Update message with new embed and buttons
            await ModifyOriginalResponseAsync(msg =>
            {
                msg.Embed = embed.Build();
                msg.Components = totalPages > 1 ? components.Build() : null;
            });

            _logger.LogDebug("Modlog page navigation completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle modlog page navigation for user {TargetUserId}", targetUserId);

            await FollowupAsync("Failed to navigate pages. Please try again.", ephemeral: true);
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
}
