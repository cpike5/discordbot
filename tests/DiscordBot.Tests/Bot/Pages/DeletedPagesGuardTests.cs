using System.Text.RegularExpressions;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Pages;

/// <summary>
/// Regression guard for the Phase 3 Blazor-port review finding: deleting a Razor Page cluster
/// (<c>Pages/Landing.cshtml</c>, <c>Pages/Search.cshtml</c>, <c>Pages/Error/*.cshtml</c>) left
/// behind an <c>asp-page="/Search"</c> tag helper and a <c>RedirectToPage("/Landing")</c> call
/// that both silently broke (an empty form action; an <c>InvalidOperationException</c> thrown at
/// request time) because the tag helper/MVC routing can't see that the target page no longer
/// exists at compile time. Model on <c>IconPathsUsageGuardTests</c>
/// (<c>tests/DiscordBot.ComponentTests/Blazor/Shared/</c>): a static sweep of the still-live
/// Razor Pages tree for any reference to a page name this or a future cluster deletion removed.
/// </summary>
public class DeletedPagesGuardTests
{
    /// <summary>
    /// Razor Page route names deleted so far during the Blazor port. Phase 4+ clusters that
    /// delete another <c>.cshtml</c>/<c>.cshtml.cs</c> pair append their own route(s) here instead
    /// of adding a new ad-hoc guard test.
    /// </summary>
    private static readonly string[] DeletedPageRoutes =
    {
        "/Landing",
        "/Search",
        "/Error/403",
        "/Error/404",
        "/Error/500",
    };

    [Fact]
    public void NoRazorPage_ReferencesADeletedPage_ByAspPageRedirectToPageOrUrlPage()
    {
        var pagesDir = Path.Combine(FindRepoRoot(), "src", "DiscordBot.Bot", "Pages");
        Directory.Exists(pagesDir).Should().BeTrue($"expected {pagesDir} to exist");

        // asp-page="/Route" / asp-page='/Route' (tag helper attribute, .cshtml only) and
        // RedirectToPage("/Route") / Url.Page("/Route" (C#, .cshtml.cs and .cshtml @code blocks
        // alike) - tolerating whitespace around "=" and either quote style, same as
        // IconPathsUsageGuardTests. Each deleted route gets its own alternation so the violation
        // message can name exactly which stale route was found.
        var patterns = DeletedPageRoutes.Select(route => new
        {
            Route = route,
            Regex = new Regex(
                $@"asp-page\s*=\s*[""']{Regex.Escape(route)}[""']" +
                $@"|RedirectToPage\s*\(\s*[""']{Regex.Escape(route)}[""']" +
                $@"|Url\.Page\s*\(\s*[""']{Regex.Escape(route)}[""']",
                RegexOptions.Compiled)
        }).ToList();

        var violations = new List<string>();
        var files = Directory.EnumerateFiles(pagesDir, "*.cshtml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(pagesDir, "*.cshtml.cs", SearchOption.AllDirectories));

        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("///", StringComparison.Ordinal)
                    || trimmed.StartsWith("//", StringComparison.Ordinal)
                    || trimmed.StartsWith("*", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var pattern in patterns)
                {
                    if (pattern.Regex.IsMatch(line))
                    {
                        violations.Add($"{Path.GetRelativePath(pagesDir, file)}:{i + 1} references deleted page \"{pattern.Route}\": {line.Trim()}");
                    }
                }
            }
        }

        violations.Should().BeEmpty(
            "every reference to a deleted Razor Page must be replaced with a literal path (e.g. " +
            "\"/landing\", \"/Search\") since the tag helper/MVC routing has no compile-time link " +
            $"to a page that no longer exists, but found:\n{string.Join('\n', violations)}");
    }

    /// <summary>
    /// Walks up from the test assembly's output directory to find the repo root (the directory
    /// containing <c>DiscordBot.sln</c>), so this test works from any build output layout without
    /// a hardcoded relative path (same helper as <c>SkillContractTests</c>/<c>IconPathsUsageGuardTests</c>).
    /// </summary>
    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DiscordBot.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests must be able to find the repository they are testing");

        return directory!.FullName;
    }
}
