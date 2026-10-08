using Discord;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// The mention policy for bot messages whose text comes from an admin template or a user (decision D8
/// in <c>docs/plans/codebase-review-fixes-2026-10.md</c>). Without an <see cref="AllowedMentions"/>
/// argument Discord parses every mention in the content, so a template or a quoted message containing
/// <c>@everyone</c>, <c>@here</c> or a role mention would ping all of them. Discord.Net 3.20 has no
/// client-wide default, so each send passes one of these. Each property returns a new instance.
/// </summary>
public static class SafeMentions
{
    /// <summary>
    /// User mentions ping; <c>@everyone</c>, <c>@here</c> and role mentions do not. For messages where
    /// pinging a user is the point: a welcome greeting, a Rat Watch call-out, a scheduled message.
    /// </summary>
    public static AllowedMentions UsersOnly => new(AllowedMentionTypes.Users);

    /// <summary>
    /// Nothing in the content pings, but a reply still notifies the author of the message it answers.
    /// With an <see cref="AllowedMentions"/> present Discord defaults <c>replied_user</c> to false, so
    /// plain <see cref="AllowedMentionTypes.None"/> on a reply would silently stop notifying the asker.
    /// </summary>
    public static AllowedMentions ReplyOnly => new(AllowedMentionTypes.None) { MentionRepliedUser = true };
}
