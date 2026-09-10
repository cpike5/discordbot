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
    /// Returns <paramref name="returnUrl"/> unless it is empty or points at a login page,
    /// in which case <paramref name="fallback"/> (normally the home page) is returned.
    /// </summary>
    public static string Sanitize(string? returnUrl, string fallback)
    {
        return string.IsNullOrWhiteSpace(returnUrl) || PointsToLoginPage(returnUrl)
            ? fallback
            : returnUrl;
    }
}
