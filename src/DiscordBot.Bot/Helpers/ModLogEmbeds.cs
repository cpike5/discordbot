using Discord;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Extensions;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Builds the embeds the mod-log channel feed posts. Pure functions over DTOs, so the tests assert on
/// fields without a Discord client and the notifier stays about delivery.
/// </summary>
public static class ModLogEmbeds
{
    /// <summary>Longest reason shown before it is cut with an ellipsis (Discord caps a field at 1024).</summary>
    public const int MaxReasonLength = 900;

    /// <summary>Longest context excerpt shown.</summary>
    public const int MaxContextLength = 300;

    /// <summary>Text shown in the Moderator field when the case was created by auto-moderation.</summary>
    public const string AutoModerationLabel = "Auto-moderation";

    /// <summary>
    /// The embed for a moderation case: colour by type, the people involved, the reason, and how
    /// long it lasts when it is temporary.
    /// </summary>
    /// <param name="moderationCase">The case.</param>
    /// <param name="botUserId">The bot's own user id, so a case the automod created says so instead of mentioning the bot.</param>
    public static Embed ForCase(ModerationCaseDto moderationCase, ulong botUserId)
    {
        var builder = new EmbedBuilder()
            .WithTitle($"Case #{moderationCase.CaseNumber} · {moderationCase.Type.DisplayName()}")
            .WithColor(ColorFor(moderationCase.Type))
            .AddField("User", $"<@{moderationCase.TargetUserId}> ({moderationCase.TargetUserId})", inline: true)
            .AddField("Moderator", moderationCase.ModeratorUserId == botUserId
                ? AutoModerationLabel
                : $"<@{moderationCase.ModeratorUserId}>", inline: true)
            .AddField("Reason", TextDisplay.Truncate(
                string.IsNullOrWhiteSpace(moderationCase.Reason) ? "No reason given" : moderationCase.Reason,
                MaxReasonLength))
            .WithFooter($"Case {moderationCase.Id}")
            .WithTimestamp(new DateTimeOffset(DateTime.SpecifyKind(moderationCase.CreatedAt, DateTimeKind.Utc)));

        if (moderationCase.Duration.HasValue)
        {
            builder.AddField("Duration", DisplayFormat.Duration(moderationCase.Duration.Value), inline: true);
        }

        if (moderationCase.ExpiresAt.HasValue)
        {
            var expires = new DateTimeOffset(DateTime.SpecifyKind(moderationCase.ExpiresAt.Value, DateTimeKind.Utc));
            builder.AddField("Expires", $"<t:{expires.ToUnixTimeSeconds()}:R>", inline: true);
        }

        if (moderationCase.ContextChannelId.HasValue && moderationCase.ContextMessageId.HasValue)
        {
            var link = $"https://discord.com/channels/{moderationCase.GuildId}/{moderationCase.ContextChannelId}/{moderationCase.ContextMessageId}";
            var excerpt = string.IsNullOrWhiteSpace(moderationCase.ContextMessageContent)
                ? string.Empty
                : "\n> " + TextDisplay.Truncate(moderationCase.ContextMessageContent, MaxContextLength).Replace("\n", "\n> ");
            builder.AddField("Context", $"[Jump to message]({link}){excerpt}");
        }

        return builder.Build();
    }

    /// <summary>
    /// The "View in portal" link button under a case embed: the member's moderation page, where the
    /// case, their other cases, notes and tags all are.
    /// </summary>
    public static MessageComponent CaseComponents(ModerationCaseDto moderationCase, string baseUrl)
        => new ComponentBuilder()
            .WithButton("View in portal", style: ButtonStyle.Link, url: PortalCaseUrl(moderationCase, baseUrl))
            .Build();

    /// <summary>The portal page a case embed links to.</summary>
    public static string PortalCaseUrl(ModerationCaseDto moderationCase, string baseUrl)
        => $"{baseUrl.TrimEnd('/')}/Guilds/{moderationCase.GuildId}/Members/{moderationCase.TargetUserId}/Moderation";

    /// <summary>Embed colour by case type: severity reads at a glance in a busy channel.</summary>
    public static Color ColorFor(CaseType type) => type switch
    {
        CaseType.Warn => Color.Gold,
        CaseType.Mute => Color.Orange,
        CaseType.Kick => Color.Orange,
        CaseType.Ban => Color.Red,
        CaseType.Unban => Color.Green,
        CaseType.Note => Color.Blue,
        _ => Color.Default
    };
}
