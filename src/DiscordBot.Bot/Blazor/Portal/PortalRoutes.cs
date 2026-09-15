using System.Text.RegularExpressions;

namespace DiscordBot.Bot.Blazor.Portal;

/// <summary>
/// Pure helper for reading a guild id out of a Portal route, mirroring
/// <c>DiscordBot.Bot.Blazor.Guilds.GuildRoutes.TryGetGuildId</c> for the Portal URL shapes
/// (<c>docs/plans/blazor-port-plan.md</c> §5 Phase 3). <c>PortalLayout</c> has no route-parameter
/// binding of its own - it wraps whatever page routed - so it needs this the same way
/// <c>GuildLayout</c> needs <c>GuildRoutes</c>.
/// </summary>
/// <remarks>
/// Two URL shapes exist today, and the id can land in either position depending on the page:
/// <c>/Portal/{Feature}/{guildId}</c> (the three real portal pages -
/// <c>/Portal/Soundboard/{id}</c>, <c>/Portal/TTS/{id}</c>, <c>/Portal/VOX/{id}</c>, per
/// <c>Pages/Portal/Shared/_PortalHeader.cshtml</c>'s tab hrefs) and <c>/Portal/{guildId}/{page}</c>
/// (the Phase 3 probe page, <c>/Portal/{guildId}/blazor-probe</c>). Both patterns are tried; the
/// id-first shape is checked first since it is the simpler, more specific match (a guild id is
/// always numeric, so trying it first never risks misreading a feature-first URL as id-first -
/// "Soundboard" cannot parse as a <see cref="ulong"/>).
/// </remarks>
public static class PortalRoutes
{
    private static readonly Regex IdFirstPattern =
        new(@"^/portal/(?<id>\d+)(?:/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FeatureFirstPattern =
        new(@"^/portal/(?<segment>[^/]+)/(?<id>\d+)(?:/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Extracts the guild id from a Portal path or absolute URI. Case-insensitive. Returns
    /// <c>false</c> for anything else (e.g. <c>/Portal</c>, a non-Portal route).
    /// </summary>
    public static bool TryGetGuildId(string pathOrUri, out ulong guildId)
    {
        guildId = 0;
        var path = ExtractPath(pathOrUri);
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var idFirst = IdFirstPattern.Match(path);
        if (idFirst.Success && ulong.TryParse(idFirst.Groups["id"].Value, out guildId))
        {
            return true;
        }

        var featureFirst = FeatureFirstPattern.Match(path);
        if (featureFirst.Success && ulong.TryParse(featureFirst.Groups["id"].Value, out guildId))
        {
            return true;
        }

        return false;
    }

    private static string ExtractPath(string pathOrUri)
    {
        if (string.IsNullOrEmpty(pathOrUri))
        {
            return string.Empty;
        }

        var path = Uri.TryCreate(pathOrUri, UriKind.Absolute, out var absolute)
            ? absolute.AbsolutePath
            : pathOrUri;

        var cut = path.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            path = path[..cut];
        }

        return path.StartsWith('/') ? path : "/" + path;
    }
}
