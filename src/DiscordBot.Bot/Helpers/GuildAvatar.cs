namespace DiscordBot.Bot.Helpers;

/// <summary>
/// The fill behind a guild's initials when it has no icon. One function, so the admin dashboard
/// and the member portal give the same guild the same colour.
/// </summary>
public static class GuildAvatar
{
    // Token classes (white text reads on every fill), whole names so Tailwind finds them
    private static readonly string[] Palette =
    [
        "bg-accent-orange",
        "bg-accent-blue",
        "bg-success",
        "bg-info",
        "bg-accent-purple"
    ];

    /// <summary>
    /// The background class for a guild: the same guild gets the same colour on every render.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    public static string ClassFor(ulong guildId) => Palette[(int)(guildId % (uint)Palette.Length)];
}
