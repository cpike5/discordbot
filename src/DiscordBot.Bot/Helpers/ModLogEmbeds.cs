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

    /// <summary>
    /// The embed for a flagged event auto-moderation raised and did not act on: what rule, how
    /// severe, who, where, and the message or account details behind it.
    /// </summary>
    public static Embed ForFlaggedEvent(FlaggedEventDto flaggedEvent, ModLogFlaggedContext context)
        => FlaggedEventBuilder(flaggedEvent, context)
            .WithTitle($"Auto-mod flagged: {flaggedEvent.RuleType.DisplayName()}")
            .WithColor(ColorFor(flaggedEvent.Severity))
            .Build();

    /// <summary>
    /// The embed for an action auto-moderation took on its own. Same body as the flagged event, with
    /// the action and whether it went through on top, so one event never posts twice.
    /// </summary>
    public static Embed ForAutoAction(FlaggedEventDto flaggedEvent, AutoAction action, bool succeeded, ModLogFlaggedContext context)
    {
        var builder = FlaggedEventBuilder(flaggedEvent, context)
            .WithTitle($"Auto-mod {ActionVerb(action)}: {flaggedEvent.RuleType.DisplayName()}")
            .WithColor(succeeded ? ColorFor(flaggedEvent.Severity) : Color.DarkGrey);

        builder.Fields.Insert(0, new EmbedFieldBuilder()
            .WithName("Action")
            .WithValue(succeeded ? ActionLabel(action) : $"{ActionLabel(action)} (failed; see the bot log)")
            .WithIsInline(true));

        return builder.Build();
    }

    /// <summary>
    /// The review buttons under a flagged event or an automatic action. The custom ids are the ones
    /// <c>FlaggedEventComponentModule</c> answers; change them together.
    /// </summary>
    public static MessageComponent FlaggedEventComponents(Guid eventId)
        => new ComponentBuilder()
            .WithButton("Dismiss", $"automod:dismiss:{eventId}", ButtonStyle.Secondary)
            .WithButton("Acknowledge", $"automod:ack:{eventId}", ButtonStyle.Primary)
            .WithButton("Take Action", $"automod:action:{eventId}", ButtonStyle.Danger)
            .Build();

    private static EmbedBuilder FlaggedEventBuilder(FlaggedEventDto flaggedEvent, ModLogFlaggedContext context)
    {
        var builder = new EmbedBuilder()
            .WithDescription(string.IsNullOrWhiteSpace(flaggedEvent.Description) ? null : flaggedEvent.Description)
            .AddField("User", $"<@{flaggedEvent.UserId}> ({flaggedEvent.UserId})", inline: true)
            .AddField("Severity", flaggedEvent.Severity.DisplayName(), inline: true)
            .WithFooter($"Event {flaggedEvent.Id}")
            .WithTimestamp(new DateTimeOffset(DateTime.SpecifyKind(flaggedEvent.CreatedAt, DateTimeKind.Utc)));

        if (flaggedEvent.ChannelId.HasValue)
        {
            builder.AddField("Channel", $"<#{flaggedEvent.ChannelId}>", inline: true);
        }

        if (!string.IsNullOrWhiteSpace(context.MessageContent))
        {
            builder.AddField("Message", TextDisplay.Truncate(context.MessageContent, MaxReasonLength));
        }

        if (context.AccountCreatedAt.HasValue)
        {
            builder.AddField("Account created", $"<t:{context.AccountCreatedAt.Value.ToUnixTimeSeconds()}:R>", inline: true);
        }

        if (context.JoinedAt.HasValue)
        {
            builder.AddField("Joined", $"<t:{context.JoinedAt.Value.ToUnixTimeSeconds()}:R>", inline: true);
        }

        return builder;
    }

    /// <summary>Past-tense verb for the title of an automatic action.</summary>
    public static string ActionVerb(AutoAction action) => action switch
    {
        AutoAction.Delete => "deleted a message",
        AutoAction.Warn => "warned",
        AutoAction.Mute => "muted",
        AutoAction.Kick => "kicked",
        AutoAction.Ban => "banned",
        _ => "acted"
    };

    /// <summary>Short label for the Action field of an automatic action.</summary>
    public static string ActionLabel(AutoAction action) => action switch
    {
        AutoAction.Delete => "Message deleted",
        AutoAction.Warn => "Warning",
        AutoAction.Mute => "Muted for 1 hour",
        AutoAction.Kick => "Kicked",
        AutoAction.Ban => "Banned",
        _ => action.ToString()
    };

    /// <summary>Embed colour by severity, the same scale the automod alert used.</summary>
    public static Color ColorFor(Severity severity) => severity switch
    {
        Severity.Low => Color.Blue,
        Severity.Medium => Color.Gold,
        Severity.High => Color.Orange,
        Severity.Critical => Color.Red,
        _ => Color.Default
    };

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
