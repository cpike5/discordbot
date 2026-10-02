namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Helpers for sanitizing post-authentication return URLs.
/// </summary>
public static class ReturnUrlHelper
{
    private static readonly string[] LoginPaths =
    {
        "/Account/Login",
        "/login"
    };

    /// <summary>
    /// Returns <c>true</c> when the return URL points back at a login page,
    /// which would either loop or fail once the user is authenticated.
    /// </summary>
    /// <param name="returnUrl">The candidate return URL.</param>
    public static bool PointsToLoginPage(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return false;
        }

        var path = returnUrl.Trim();
        if (path.StartsWith("~/", StringComparison.Ordinal))
        {
            path = path[1..];
        }

        var queryIndex = path.IndexOfAny(new[] { '?', '#' });
        if (queryIndex >= 0)
        {
            path = path[..queryIndex];
        }

        path = path.TrimEnd('/');

        foreach (var loginPath in LoginPaths)
        {
            if (string.Equals(path, loginPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> when the URL stays on this site: an app-relative path
    /// (<c>/path</c> or <c>~/path</c>), never a protocol-relative (<c>//host</c>),
    /// backslash (<c>/\host</c>), absolute or <c>javascript:</c> URL. Same rules as
    /// <c>IUrlHelper.IsLocalUrl</c>, usable where no URL helper is at hand.
    /// </summary>
    /// <param name="url">The candidate URL.</param>
    public static bool IsLocalUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return false;
        }

        if (url[0] == '/')
        {
            // "/" alone, or "/x" where x is neither '/' nor '\'
            return url.Length == 1 || (url[1] != '/' && url[1] != '\\' && !HasControlCharacter(url.AsSpan(1)));
        }

        if (url[0] == '~' && url.Length > 1 && url[1] == '/')
        {
            // "~/" alone, or "~/x" where x is neither '/' nor '\'
            return url.Length == 2 || (url[2] != '/' && url[2] != '\\' && !HasControlCharacter(url.AsSpan(2)));
        }

        return false;
    }

    /// <summary>
    /// Returns <paramref name="returnUrl"/> unless it is empty, not a local URL, or points
    /// at a login page, in which case <paramref name="fallback"/> (normally the home page)
    /// is returned. The result is always safe for <c>LocalRedirect</c> and for an
    /// <c>href</c>, provided the fallback is.
    /// </summary>
    public static string Sanitize(string? returnUrl, string fallback)
    {
        return string.IsNullOrWhiteSpace(returnUrl) || !IsLocalUrl(returnUrl) || PointsToLoginPage(returnUrl)
            ? fallback
            : returnUrl;
    }

    private static bool HasControlCharacter(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            if (char.IsControl(c))
            {
                return true;
            }
        }

        return false;
    }
}
