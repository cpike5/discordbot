using DiscordBot.Bot.Helpers;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace DiscordBot.Bot.TagHelpers;

/// <summary>
/// Resolves the theme for the request and stamps it on the root element, so the first paint
/// is already in the right theme. Pair it with <c>&lt;partial name="_ThemeHead" /&gt;</c> in the head.
/// </summary>
/// <example>
/// <html lang="en" theme-root>
/// </example>
/// <remarks>
/// Writes <c>data-theme</c> (the theme key) and <c>data-theme-saved</c> ("true" when the visitor
/// chose the theme; otherwise the head script follows <c>prefers-color-scheme</c>). A failure to
/// resolve is logged and leaves the stylesheet default, because the error page renders through
/// this too and must work when the database is what failed.
/// </remarks>
[HtmlTargetElement("html", Attributes = "theme-root")]
public class ThemeRootTagHelper : TagHelper
{
    private readonly IThemeService _themeService;
    private readonly ILogger<ThemeRootTagHelper> _logger;

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public ThemeRootTagHelper(IThemeService themeService, ILogger<ThemeRootTagHelper> logger)
    {
        _themeService = themeService;
        _logger = logger;
    }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        output.Attributes.RemoveAll("theme-root");

        var httpContext = ViewContext.HttpContext;
        try
        {
            var theme = await _themeService.GetCurrentThemeAsync(httpContext.RequestAborted);
            httpContext.Items[ThemeAppearance.HttpContextItemKey] = theme;
            output.Attributes.SetAttribute("data-theme", theme.Theme.ThemeKey);
            output.Attributes.SetAttribute("data-theme-saved", ThemeAppearance.IsSaved(theme) ? "true" : "false");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not resolve the theme for {Path}; using the stylesheet default", httpContext.Request.Path);
            output.Attributes.SetAttribute("data-theme-saved", "false");
        }
    }
}
