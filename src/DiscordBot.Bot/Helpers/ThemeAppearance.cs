using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// What the browser chrome needs to know about each theme: which key is the dark one and which
/// the light one (so a visitor with no saved choice can follow <c>prefers-color-scheme</c>), and
/// the colour for <c>&lt;meta name="theme-color"&gt;</c> before the stylesheet loads.
/// </summary>
/// <remarks>
/// Rendered by <c>Pages/Shared/_ThemeHead.cshtml</c> and the <c>theme-root</c> tag helper; the
/// browser side is <c>wwwroot/js/theme.js</c>. There are two themes (UX decision D5); a third
/// would need a mode here and its own <c>[data-theme]</c> block in <c>site.css</c>.
/// </remarks>
public static class ThemeAppearance
{
    /// <summary>Key of the dark theme, Graphite (the <c>:root</c> tokens in <c>site.css</c>).</summary>
    public const string DarkThemeKey = "discord-dark";

    /// <summary>Key of the light theme, Purple Dusk.</summary>
    public const string LightThemeKey = "purple-dusk";

    /// <summary>The <c>HttpContext.Items</c> key holding the theme resolved for this request.</summary>
    public const string HttpContextItemKey = "DiscordBot.CurrentTheme";

    /// <summary>
    /// The browser UI colour for a theme: its <c>--color-bg-primary</c> in <c>site.css</c>.
    /// <c>DesignTokenContrastTests</c> keeps the two in step.
    /// </summary>
    /// <param name="themeKey">The theme key, or null when none was resolved.</param>
    public static string BrowserColor(string? themeKey) =>
        themeKey == LightThemeKey ? "#ebe6e2" : "#0f1114";

    /// <summary>Whether the resolved theme is the visitor's own saved choice.</summary>
    /// <param name="theme">The theme resolved for this request, or null when it could not be.</param>
    public static bool IsSaved(CurrentThemeDto? theme) => theme?.Source == ThemeSource.User;

    /// <summary>The theme resolved for this request by the <c>theme-root</c> tag helper, if any.</summary>
    /// <param name="httpContext">The current request.</param>
    public static CurrentThemeDto? Current(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(HttpContextItemKey, out var value) ? value as CurrentThemeDto : null;
}
