using Microsoft.AspNetCore.Components;

namespace DiscordBot.Bot.Blazor.Layout;

/// <summary>
/// Active-link matching for <see cref="MainSidebar"/> and <see cref="MainNavbar"/>. Mirrors the
/// exact/prefix rules <c>Pages/Shared/_Sidebar.cshtml</c> computed from
/// <c>ViewContext.RouteData.Values["page"]</c> (a Razor Pages route value), now computed from
/// <see cref="NavigationManager"/>'s current URL path instead - a Blazor component has no Razor
/// Pages page route value to read. Case-insensitive throughout, matching the legacy comparisons'
/// <see cref="StringComparison.OrdinalIgnoreCase"/>. Kept as plain static methods over strings
/// (no bUnit/DI needed) so <c>ShellNavigationTests</c> can unit test the matching rules directly.
/// </summary>
public static class ShellNavigation
{
    /// <summary>
    /// Returns <paramref name="navigation"/>'s current URL path, "/"-rooted, with no query string
    /// or fragment - e.g. <c>"/Admin/Settings"</c> for <c>https://host/Admin/Settings?tab=ai</c>.
    /// </summary>
    public static string GetCurrentPath(NavigationManager navigation)
    {
        var relative = navigation.ToBaseRelativePath(navigation.Uri);
        var path = relative.Split('?', 2)[0].Split('#', 2)[0];
        return Normalize("/" + path);
    }

    /// <summary>
    /// True when <paramref name="currentPath"/> equals any of <paramref name="exact"/>, or starts
    /// with any of <paramref name="prefixes"/> - both case-insensitive. A trailing <c>"/"</c> on
    /// <paramref name="currentPath"/> (besides the bare root) is ignored, so <c>"/guilds"</c> and
    /// <c>"/guilds/"</c> match the same rule. Passing neither list is never active (matches the
    /// legacy code's behaviour of not marking a link active for a route it never checked).
    /// </summary>
    public static bool IsActive(
        string currentPath,
        IReadOnlyList<string>? exact = null,
        IReadOnlyList<string>? prefixes = null)
    {
        var normalized = Normalize(currentPath);

        if (exact is not null)
        {
            foreach (var candidate in exact)
            {
                if (string.Equals(normalized, Normalize(candidate), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        if (prefixes is not null)
        {
            foreach (var prefix in prefixes)
            {
                if (normalized.StartsWith(Normalize(prefix), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string Normalize(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "/";
        }

        return path.Length > 1 ? path.TrimEnd('/') : path;
    }
}
