namespace DiscordBot.Bot.Helpers;

/// <summary>
/// The size measures the per-server SSML limits are checked against, shared by every entry point
/// that accepts raw SSML (portal API and admin page) so they cannot disagree.
/// </summary>
public static class SsmlLimits
{
    /// <summary>
    /// A rough complexity score: the number of opening tags. Compared with
    /// <c>GuildTtsSettings.MaxSsmlComplexity</c>.
    /// </summary>
    public static int Complexity(string ssml) => ssml.Split('<').Length - 1;
}
