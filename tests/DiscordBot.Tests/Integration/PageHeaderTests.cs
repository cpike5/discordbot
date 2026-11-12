using System.Text.RegularExpressions;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// UI audit § 10: every top-level page draws its header with <c>_PageHeader</c>, has exactly one
/// <c>&lt;h1&gt;</c>, and shows no breadcrumb (it is one level below Home); the breadcrumb, typed
/// actions and free-form actions render when given (the /Components showcase). Runs against the
/// real app (<see cref="OfflineAppHost"/>).
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class PageHeaderTests : IClassFixture<PageHeaderTests.AppFixture>
{
    private readonly AppFixture _app;

    public PageHeaderTests(AppFixture app)
    {
        _app = app;
    }

    [Theory]
    [InlineData("/", "Dashboard", "Overview")]
    [InlineData("/Guilds", "Servers", "Overview")]
    [InlineData("/Commands", "Commands", "Overview")]
    [InlineData("/Admin/Performance", "Bot Performance", "Overview")]
    [InlineData("/Admin/RatWatchAnalytics", "Global Rat Watch Analytics", "Overview")]
    [InlineData("/Admin/Settings", "Settings", "Overview")]
    [InlineData("/Admin/Users", "Users", "Administration")]
    [InlineData("/Admin/Logs", "Logs", "Administration")]
    [InlineData("/Admin/Notifications", "Notifications", "Administration")]
    [InlineData("/Admin/LlmUsage", "LLM Usage", "Administration")]
    [InlineData("/Admin/Currency", "Bot-wide currency", "Administration")]
    [InlineData("/Admin/BulkPurge", "Bulk Data Purge", "Administration")]
    [InlineData("/Admin/UserPurge", "User Data Purge", "Administration")]
    public async Task TopLevelPage_HasOnePageHeader_OneH1_AndNoBreadcrumb(string route, string title, string eyebrow)
    {
        var html = await _app.Host.Client.GetStringAsync(route);

        Regex.Matches(html, "<h1[\\s>]").Count.Should().Be(1, "every page has exactly one h1");
        html.Should().Contain($"<h1 class=\"page-title\">{title}</h1>");
        Regex.Matches(html, "data-page-header").Count.Should().Be(1);
        html.Should().Contain($"<p class=\"page-eyebrow\">{eyebrow}</p>", "the eyebrow names the sidebar group");
        html.Should().NotContain("aria-label=\"Breadcrumb\"", "a page one level below Home shows no breadcrumb");
        html.Should().NotContain("max-w-7xl mx-auto px-4", "the layout's page container sets the width");
    }

    [Fact]
    public async Task Components_ShowsEveryHeaderCombination_AndKeepsOneH1()
    {
        var html = await _app.Host.Client.GetStringAsync("/Components");

        Regex.Matches(html, "<h1[\\s>]").Count.Should().Be(1, "the showcase examples use HeadingLevel 2");
        html.Should().Contain("<h1 class=\"page-title\">Component Showcase</h1>");

        // Breadcrumb only for the example more than one level deep, current page marked
        Regex.Matches(html, "aria-label=\"Breadcrumb\"").Count.Should().Be(1);
        html.Should().Contain("<span class=\"breadcrumb-current\" aria-current=\"page\">Members</span>");
        html.Should().Contain("<a href=\"/Guilds\" class=\"breadcrumb-link\">Servers</a>");

        // Typed actions, free-form actions and the badge
        html.Should().MatchRegex("<a href=\"#primary\"\\s+class=\"btn btn-accent");
        html.Should().MatchRegex("<a href=\"#secondary\"\\s+class=\"btn btn-secondary");
        html.Should().Contain("Free-form action content");
        html.Should().Contain("data-page-badge");
    }

    [Fact]
    public async Task Settings_UsesUnderlineTabs_AndPerformanceAndLogsUsePills()
    {
        (await _app.Host.Client.GetStringAsync("/Admin/Settings")).Should().Contain("tab-panel-underline",
            "Settings' categories are sections of one page");
        (await _app.Host.Client.GetStringAsync("/Admin/Performance")).Should().Contain("tab-panel-pills",
            "each Performance tab is its own ?tab= address");
        (await _app.Host.Client.GetStringAsync("/Admin/Logs")).Should().Contain("tab-panel-pills",
            "each Logs tab is its own page load");
    }

    [Fact]
    public async Task Commands_SubtitleCarriesTheHookCommandTabsUpdates()
    {
        var html = await _app.Host.Client.GetStringAsync("/Commands?tab=analytics");

        html.Should().Contain("<p class=\"page-subtitle\" data-page-subtitle>Usage statistics and performance</p>");
    }

    public sealed class AppFixture : IAsyncLifetime
    {
        public OfflineAppHost Host { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            Host = await OfflineAppHost.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await Host.DisposeAsync();
        }
    }
}
