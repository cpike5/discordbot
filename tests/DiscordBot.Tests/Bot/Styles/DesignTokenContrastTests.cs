using System.Globalization;
using System.Text.RegularExpressions;
using DiscordBot.Bot.Helpers;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Styles;

/// <summary>
/// Reads the colour tokens out of <c>wwwroot/css/site.css</c> and checks every text-on-surface
/// pairing the components use against WCAG 2.1 AA (4.5:1) in each theme. A token change that
/// breaks a pairing fails here, with the pair and its ratio, before anyone has to open a browser.
/// </summary>
public class DesignTokenContrastTests
{
    private const double MinimumRatio = 4.5;

    private static readonly string[] Surfaces = ["bg-primary", "bg-secondary", "bg-tertiary", "bg-inset"];
    private static readonly string[] TextTokens = ["text-primary", "text-secondary", "text-tertiary"];

    /// <summary>Colours used as text (inks) that also have a fill for solid backgrounds.</summary>
    private static readonly string[] Accents =
        ["accent-orange", "accent-blue", "accent-purple", "success", "warning", "error", "info"];

    /// <summary>The tint alpha behind badges, alerts and <c>bg-x-bg</c> utilities.</summary>
    private const double TintAlpha = 0.12;

    public static TheoryData<string> Themes => new() { "graphite", "purple-dusk" };

    [Theory]
    [MemberData(nameof(Themes))]
    public void EveryTokenPairingMeetsWcagAA(string theme)
    {
        var tokens = LoadTheme(theme);
        var failures = new List<string>();

        void Check(string description, Rgb foreground, Rgb background)
        {
            var ratio = ContrastRatio(foreground, background);
            if (ratio < MinimumRatio)
            {
                failures.Add($"{description}: {ratio.ToString("0.00", CultureInfo.InvariantCulture)}:1");
            }
        }

        foreach (var text in TextTokens)
        {
            foreach (var surface in Surfaces)
            {
                Check($"{text} on {surface}", tokens[text], tokens[surface]);
            }
        }

        foreach (var surface in new[] { "bg-inset", "bg-primary", "bg-secondary" })
        {
            Check($"text-placeholder on {surface}", tokens["text-placeholder"], tokens[surface]);
        }

        foreach (var accent in Accents)
        {
            foreach (var surface in Surfaces)
            {
                Check($"{accent} text on {surface}", tokens[accent], tokens[surface]);
            }

            foreach (var surface in new[] { "bg-primary", "bg-secondary" })
            {
                var tint = Blend(tokens[$"{accent}-fill"], tokens[surface], TintAlpha);
                Check($"{accent} text on its tint over {surface}", tokens[accent], tint);
            }

            var onFill = accent == "warning" ? "on-warning" : "text-inverse";
            foreach (var state in new[] { "", "-hover", "-active" })
            {
                var fill = $"{accent}-fill{state}";
                Check($"{onFill} on {fill}", tokens[onFill], tokens[fill]);
            }
        }

        failures.Should().BeEmpty($"every pairing in the {theme} theme must reach {MinimumRatio}:1");
    }

    [Theory]
    [InlineData("graphite", ThemeAppearance.DarkThemeKey)]
    [InlineData("purple-dusk", ThemeAppearance.LightThemeKey)]
    public void BrowserThemeColourIsTheThemesCanvas(string theme, string themeKey)
    {
        // <meta name="theme-color"> is rendered before the stylesheet, so it carries a copy
        var canvas = LoadTheme(theme)["bg-primary"];

        Rgb.Parse(ThemeAppearance.BrowserColor(themeKey)).Should().Be(canvas);
    }

    [Fact]
    public void ContrastRatioMatchesTheWcagReferenceValues()
    {
        ContrastRatio(Rgb.Parse("#000000"), Rgb.Parse("#ffffff")).Should().BeApproximately(21, 0.01);
        ContrastRatio(Rgb.Parse("#777777"), Rgb.Parse("#ffffff")).Should().BeApproximately(4.48, 0.01);
    }

    private static Dictionary<string, Rgb> LoadTheme(string theme)
    {
        var css = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "DiscordBot.Bot", "wwwroot", "css", "site.css"));

        // The default theme is the :root block; others override it on [data-theme="…"]
        var tokens = ReadHexTokens(Block(css, ":root {"));
        if (theme != "graphite")
        {
            foreach (var (name, value) in ReadHexTokens(Block(css, $"[data-theme=\"{theme}\"] {{")))
            {
                tokens[name] = value;
            }
        }

        return tokens;
    }

    private static string Block(string css, string opener)
    {
        var start = css.IndexOf(opener, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0, $"site.css must contain a `{opener}` block");
        var end = css.IndexOf("\n}", start, StringComparison.Ordinal);
        return css[start..end];
    }

    private static Dictionary<string, Rgb> ReadHexTokens(string block)
    {
        var tokens = new Dictionary<string, Rgb>();
        foreach (Match match in Regex.Matches(block, @"--color-([a-z0-9-]+):\s*(#[0-9a-fA-F]{6})\s*;"))
        {
            tokens[match.Groups[1].Value] = Rgb.Parse(match.Groups[2].Value);
        }

        return tokens;
    }

    private static Rgb Blend(Rgb foreground, Rgb background, double alpha) => new(
        foreground.R * alpha + background.R * (1 - alpha),
        foreground.G * alpha + background.G * (1 - alpha),
        foreground.B * alpha + background.B * (1 - alpha));

    private static double ContrastRatio(Rgb a, Rgb b)
    {
        var lighter = Math.Max(a.Luminance, b.Luminance);
        var darker = Math.Min(a.Luminance, b.Luminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DiscordBot.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests must be able to find the repository they are testing");

        return directory!.FullName;
    }

    private readonly record struct Rgb(double R, double G, double B)
    {
        public static Rgb Parse(string hex) => new(
            Convert.ToInt32(hex.Substring(1, 2), 16),
            Convert.ToInt32(hex.Substring(3, 2), 16),
            Convert.ToInt32(hex.Substring(5, 2), 16));

        public double Luminance => 0.2126 * Channel(R) + 0.7152 * Channel(G) + 0.0722 * Channel(B);

        private static double Channel(double value)
        {
            var c = value / 255;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
    }
}
