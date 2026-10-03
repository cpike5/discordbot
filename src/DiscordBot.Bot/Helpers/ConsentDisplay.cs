using DiscordBot.Core.Extensions;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// How the consent history names where a change was made. <c>GrantedVia</c> / <c>RevokedVia</c> are
/// stored as short codes ("WebUI", "SlashCommand"); a person reads "the web portal" (UX plan C-2).
/// </summary>
public static class ConsentDisplay
{
    /// <summary>The place a consent change was made, ready to follow the word "via".</summary>
    public static string Via(string? source) => source switch
    {
        null or "" => "an unknown source",
        "WebUI" => "the web portal",
        "SlashCommand" => "a Discord command",
        _ => EnumDisplayExtensions.Humanize(source).ToLowerInvariant()
    };
}
