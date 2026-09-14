using System.Text.RegularExpressions;
using DiscordBot.Bot.Configuration;

namespace DiscordBot.Bot.Blazor.Guilds;

/// <summary>
/// Pure helpers for reading a guild id and the active nav tab out of a route, so a static
/// <c>GuildLayout</c> (which has no route-parameter binding of its own - it wraps whatever page
/// routed) can obtain both from <c>NavigationManager.Uri</c>. Matches the legacy URL shapes
/// <c>GuildNavigationConfig</c> still emits (<c>docs/plans/blazor-port-plan.md</c> §5 Phase 3):
/// <c>/Guilds/{guildId}/...</c> (guild id right after <c>/Guilds/</c>, e.g. Members, Currency,
/// FeatureRequests) and <c>/Guilds/&lt;Page&gt;/{guildId}</c> (a page-name segment first, e.g.
/// Details, Soundboard, RatWatch). The two never collide because a guild id is always numeric and
/// a page-name segment never is.
/// </summary>
public static class GuildRoutes
{
    private static readonly Regex DirectIdPattern =
        new(@"^/guilds/(?<id>\d+)(?:/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NamedPageIdPattern =
        new(@"^/guilds/(?<segment>[^/]+)/(?<id>\d+)(?:/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Extracts the guild id from a path or absolute URI matching one of the two shapes above.
    /// Case-insensitive. Returns <c>false</c> for anything else (e.g. <c>/Guilds</c>,
    /// <c>/Guilds/Index</c>, a non-guild route).
    /// </summary>
    public static bool TryGetGuildId(string pathOrUri, out ulong guildId)
    {
        guildId = 0;
        var path = ExtractPath(pathOrUri);
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var direct = DirectIdPattern.Match(path);
        if (direct.Success && ulong.TryParse(direct.Groups["id"].Value, out guildId))
        {
            return true;
        }

        var named = NamedPageIdPattern.Match(path);
        if (named.Success && ulong.TryParse(named.Groups["id"].Value, out guildId))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Finds the tab in <c>GuildNavigationConfig.GetTabs()</c> whose <c>GetUrl(guildId)</c> is
    /// the longest prefix of <paramref name="path"/> - so a sub-page under a tab (e.g.
    /// <c>/Guilds/{id}/Currency/Details/5</c> under the <c>currency</c> tab's
    /// <c>/Guilds/{id}/Currency</c>) still resolves to that tab as active. Comparison is
    /// segment-aligned (a prefix match must land on a full path segment, not a partial one) and
    /// case-insensitive. The Overview tab's URL is exactly <c>/Guilds/Details/{id}</c>, so only
    /// that literal path (or a sub-path under it) resolves to <c>"overview"</c> - a bare
    /// <c>/Guilds/{id}</c> with nothing after it matches no tab at all, since no tab is registered
    /// at that URL.
    /// </summary>
    public static string? ResolveActiveTabId(string path, ulong guildId)
    {
        var normalizedPath = ExtractPath(path).TrimEnd('/');
        if (normalizedPath.Length == 0)
        {
            return null;
        }

        string? bestTabId = null;
        var bestLength = -1;

        foreach (var tab in GuildNavigationConfig.GetTabs())
        {
            var tabUrl = tab.GetUrl(guildId).TrimEnd('/');
            var isMatch = normalizedPath.Equals(tabUrl, StringComparison.OrdinalIgnoreCase)
                || normalizedPath.StartsWith(tabUrl + "/", StringComparison.OrdinalIgnoreCase);

            if (isMatch && tabUrl.Length > bestLength)
            {
                bestLength = tabUrl.Length;
                bestTabId = tab.Id;
            }
        }

        return bestTabId;
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
