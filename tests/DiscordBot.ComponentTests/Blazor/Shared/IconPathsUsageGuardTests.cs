using System.Text.RegularExpressions;
using Bunit;
using DiscordBot.Bot.Blazor.Pages.Components.Sections;
using DiscordBot.Bot.Interfaces;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Shared;

/// <summary>
/// Regression guard for the bug fixed in docs/plans/blazor-port-plan.md §5 Phase 2 step 2: an
/// <c>&lt;Icon&gt;</c> (or any other component) attribute of the form <c>Path="IconPaths.X"</c>
/// looks like a C# expression but is a plain <c>string</c> parameter, so Razor renders the
/// literal text <c>"IconPaths.X"</c> as the SVG <c>d</c> path instead of evaluating the constant -
/// only the <c>@</c>-prefixed form (<c>Path="@IconPaths.X"</c>) is a C# expression. There were 52
/// such unguarded occurrences across the six component tiers by the time Tiers 3 and 5 landed (42
/// before them); this class catches any that come back.
/// </summary>
public class IconPathsUsageGuardTests : BlazorComponentTestContext
{
    /// <summary>
    /// Static sweep: every <c>.razor</c> AND <c>.cs</c> file under <c>Blazor/</c>, scanned for an
    /// attribute/assignment value that starts with <c>IconPaths.</c> but isn't preceded by
    /// <c>@</c> (allowing for optional whitespace and either quote style around the <c>=</c>,
    /// e.g. <c>Path="IconPaths.X"</c>, <c>Path = "IconPaths.X"</c>, or <c>Path='IconPaths.X'</c>
    /// vs. the correct <c>Path="@IconPaths.X"</c>). <c>.cs</c> files are included because a
    /// code-behind partial (<c>*.razor.cs</c>) can build the same string literal in C# with the
    /// identical bug - a bare <c>"IconPaths.X"</c> string is still just text there, not a
    /// reference to the constant. Reports every offending file:line rather than stopping at the
    /// first one, so a regression sweep fixes everything in one pass.
    /// </summary>
    [Fact]
    public void NoBlazorFile_HasAnUnguardedIconPathsAttribute()
    {
        var blazorDir = Path.Combine(FindRepoRoot(), "src", "DiscordBot.Bot", "Blazor");
        Directory.Exists(blazorDir).Should().BeTrue($"expected {blazorDir} to exist");

        // Matches `SomeAttribute="IconPaths.X` / `SomeAttribute = 'IconPaths.X` - i.e. an
        // assignment whose value starts with the literal text IconPaths. with no leading @,
        // tolerating whitespace around `=` and either quote character. A correctly-guarded usage
        // is `="@IconPaths.X"`, which this pattern does not match because the required quote
        // must sit directly before `IconPaths.` (an `@` in between breaks the match).
        var unguarded = new Regex("=\\s*[\"']IconPaths\\.", RegexOptions.Compiled);

        var violations = new List<string>();
        var files = Directory.EnumerateFiles(blazorDir, "*.razor", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(blazorDir, "*.cs", SearchOption.AllDirectories));
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                // Skip XML doc comments (///) and Razor comments (@* ... *@ content) - this guard
                // is about real markup/code, not a worked example inside documentation prose.
                if (lines[i].TrimStart().StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                if (unguarded.IsMatch(lines[i]))
                {
                    violations.Add($"{Path.GetRelativePath(blazorDir, file)}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        violations.Should().BeEmpty(
            "every IconPaths.X value must be @-prefixed (in .razor markup) or referenced as the " +
            "constant itself (in .cs) to evaluate as C# rather than render/compare literally, but " +
            $"found:\n{string.Join('\n', violations)}");
    }

    /// <summary>
    /// Dynamic sweep: render each of the six Phase 2 showcase sections and assert every
    /// <c>&lt;Icon&gt;</c>'s rendered <c>&lt;path d="..."&gt;</c> is real SVG path data (starts
    /// with an SVG path command, in practice always "M" for this 24x24 outline family) rather
    /// than the literal string "IconPaths.X" a missing "@" would have produced. Catches the case
    /// the static sweep above can't: a value built from a C# expression/ternary at runtime.
    /// </summary>
    [Fact]
    public void AllShowcaseSections_RenderIcons_WithRealPathData()
    {
        AddAuthorizedAdmin();

        // WidgetsShowcase composes real widgets (BotStatusBanner/Card, NotificationBell,
        // VoiceChannelPanel) that inject live services - bUnit's container needs something
        // resolvable for each, matching the setups those widgets' own component tests use
        // (Blazor/Shared/Widgets/*Tests.cs). AddAuthorizedAdmin's identity carries no
        // NameIdentifier claim, so NotificationBell's OnInitializedAsync returns before ever
        // calling IDashboardNotificationQueryService - it only needs to resolve from DI.
        var metricsService = new Mock<IDashboardMetricsService>();
        metricsService
            .Setup(m => m.GetCurrentStatus(null, null))
            .Returns(new BotStatusDto { ConnectionState = "Connected", GuildCount = 1, LatencyMs = 10, Uptime = TimeSpan.FromMinutes(5) });
        var guildService = new Mock<IGuildService>();
        guildService
            .Setup(g => g.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GuildDto>());
        var versionService = new Mock<IVersionService>();
        versionService.Setup(v => v.GetVersion()).Returns("v0.0.0-test");
        var audioStatusService = new Mock<IDashboardAudioStatusService>();
        audioStatusService
            .Setup(s => s.GetCurrentAudioStatus(123456789012345678UL, null, null))
            .Returns(new AudioStatusDto { GuildId = 123456789012345678UL, IsConnected = false });

        Services.AddSingleton(metricsService.Object);
        Services.AddSingleton(guildService.Object);
        Services.AddSingleton(versionService.Object);
        Services.AddSingleton(audioStatusService.Object);
        Services.AddSingleton(Mock.Of<IDashboardNotificationQueryService>());
        Services.AddSingleton(Mock.Of<IAudioService>());
        Services.AddSingleton(Mock.Of<IPlaybackService>());

        AssertNoLiteralIconPathsInIcons(Render<PrimitivesShowcase>().FindAll("path"), nameof(PrimitivesShowcase));
        AssertNoLiteralIconPathsInIcons(Render<StatusAndHeadersShowcase>().FindAll("path"), nameof(StatusAndHeadersShowcase));
        AssertNoLiteralIconPathsInIcons(Render<FormsShowcase>().FindAll("path"), nameof(FormsShowcase));
        AssertNoLiteralIconPathsInIcons(Render<NavigationAndOverlaysShowcase>().FindAll("path"), nameof(NavigationAndOverlaysShowcase));
        AssertNoLiteralIconPathsInIcons(Render<WidgetsShowcase>().FindAll("path"), nameof(WidgetsShowcase));
        AssertNoLiteralIconPathsInIcons(Render<TtsShowcase>().FindAll("path"), nameof(TtsShowcase));
    }

    private static void AssertNoLiteralIconPathsInIcons(IEnumerable<AngleSharp.Dom.IElement> paths, string sectionName)
    {
        var badPaths = paths
            .Select(p => p.GetAttribute("d"))
            .Where(d => d is not null && d.StartsWith("IconPaths", StringComparison.Ordinal))
            .ToList();

        badPaths.Should().BeEmpty($"{sectionName} rendered an unevaluated IconPaths.X literal as a path 'd' attribute");
    }

    /// <summary>
    /// Walks up from the test assembly's output directory to find the repo root (the directory
    /// containing <c>DiscordBot.sln</c>), so this test works from any build output layout
    /// (local <c>bin/Debug/net10.0</c>, CI, etc.) without a hardcoded relative path.
    /// </summary>
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DiscordBot.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException(
                $"Could not find DiscordBot.sln by walking up from {AppContext.BaseDirectory}");
        }

        return dir.FullName;
    }
}
