using System.Net;
using System.Text.RegularExpressions;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Integration;

/// <summary>
/// UX plan Phase 13 (D12): the Performance dashboard shell is the one UI. The five standalone
/// pages redirect to its <c>?tab=</c> addresses, the shell opens the tab the address names, a bad
/// range is clamped, and every tab renders on a fresh install (nothing recorded yet).
/// Runs against the real app (<see cref="OfflineAppHost"/>).
/// </summary>
[Collection(OfflineAppHostCollection.Name)]
public class PerformanceShellTests : IClassFixture<PerformanceShellTests.AppFixture>
{
    private readonly AppFixture _app;

    public PerformanceShellTests(AppFixture app)
    {
        _app = app;
    }

    [Theory]
    [InlineData("/Admin/Performance/SystemHealth", "system")]
    [InlineData("/Admin/Performance/ApiMetrics", "api")]
    [InlineData("/Admin/Performance/HealthMetrics", "health")]
    [InlineData("/Admin/Performance/Alerts", "alerts")]
    [InlineData("/Admin/Performance/Commands", "commands")]
    public async Task RetiredPage_RedirectsToItsTabInTheShell(string oldRoute, string tab)
    {
        var response = await _app.Host.Client.GetAsync(oldRoute);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var landed = response.RequestMessage!.RequestUri!;
        landed.AbsolutePath.Should().Be("/Admin/Performance");
        landed.Query.Should().Be($"?tab={tab}");

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain($"data-initial-tab=\"{tab}\"");
        Regex.IsMatch(html, $"id=\"performanceTabs-tab-{tab}\"\\s+role=\"tab\"\\s+aria-selected=\"true\"")
            .Should().BeTrue("the tab the link named is the selected one at first paint");
    }

    [Fact]
    public async Task RetiredPage_ForwardsAClampedRange()
    {
        var response = await _app.Host.Client.GetAsync("/Admin/Performance/Commands?hours=100000");

        response.RequestMessage!.RequestUri!.Query.Should().Be("?tab=commands&hours=720");
        (await response.Content.ReadAsStringAsync()).Should().Contain("data-initial-hours=\"720\"");
    }

    [Fact]
    public async Task Shell_ClampsAnOutOfRangeHours()
    {
        var html = await _app.Host.Client.GetStringAsync("/Admin/Performance?tab=health&hours=-5");

        html.Should().Contain("data-initial-hours=\"24\"");
    }

    [Fact]
    public async Task Shell_OpensOverviewForAnUnknownTab()
    {
        var html = await _app.Host.Client.GetStringAsync("/Admin/Performance?tab=bogus");

        html.Should().Contain("data-initial-tab=\"overview\"");
    }

    [Fact]
    public async Task Shell_HasNoStaticLiveBadgeAndNoLiveRegionOnTheTabContent()
    {
        var html = await _app.Host.Client.GetStringAsync("/Admin/Performance");

        // "Live" is a chip a script reveals only on a tab subscribed to a hub group
        html.Should().Contain("id=\"perfLive\"");
        Regex.IsMatch(html, "<span class=\"live-indicator hidden\" id=\"perfLive\"").Should().BeTrue("hidden until a tab subscribes");
        html.Should().Contain("data-stale-badge");

        var tabContent = Regex.Match(html, "<div[^>]*id=\"tabContent\"[^>]*>").Value;
        tabContent.Should().NotBeEmpty();
        tabContent.Should().NotContain("aria-live");
        html.Should().Contain("id=\"perfStatusLine\"");
    }

    [Theory]
    [InlineData("overview")]
    [InlineData("health")]
    [InlineData("commands")]
    [InlineData("api")]
    [InlineData("system")]
    [InlineData("alerts")]
    public async Task EveryTab_RendersOnAFreshInstall(string tab)
    {
        var response = await _app.Host.Client.GetAsync($"/Admin/Performance?handler=Partial&tabId={tab}&hours=24");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain($"data-tab=\"{tab}\"");
        html.Should().NotContain("aria-live", "announcing a whole tab (or each alert card) is noise; the page has one status line");
    }

    [Theory]
    [InlineData("hours=99999")]
    [InlineData("hours=-1")]
    [InlineData("hours=abc")]
    public async Task TabPartial_AcceptsABadRangeInsteadOfFailing(string query)
    {
        var response = await _app.Host.Client.GetAsync($"/Admin/Performance?handler=Partial&tabId=commands&{query}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TabPartial_ForAnUnknownTab_Is404()
    {
        var response = await _app.Host.Client.GetAsync("/Admin/Performance?handler=Partial&tabId=bogus");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Overview_ShowsNoInventedTrends()
    {
        var html = await _app.Host.Client.GetStringAsync("/Admin/Performance?handler=Partial&tabId=overview");

        html.Should().NotContain("metric-trend", "there is no previous period to compare with");
        html.Should().NotContain("overviewResponseTimeChart", "the server keeps no response-time series, so the chart was invented");
        html.Should().NotContain("coming soon");
    }

    [Fact]
    public async Task Commands_ShowsNoInventedTrends()
    {
        var html = await _app.Host.Client.GetStringAsync("/Admin/Performance?handler=Partial&tabId=commands");

        html.Should().NotContain("No change");
        html.Should().NotContain("vs yesterday");
    }

    [Fact]
    public async Task Alerts_ThresholdsAreOneFormWithAnErrorSlotPerMetric()
    {
        var html = await _app.Host.Client.GetStringAsync("/Admin/Performance?handler=Partial&tabId=alerts");

        html.Should().Contain("id=\"alertThresholdForm\"");
        html.Should().Contain("data-threshold-error");
        html.Should().NotContain("onclick=", "handlers are wired by the tab's script, not inline");
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
