using DiscordBot.Bot.Services.Performance;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Bot.Services.Performance;

/// <summary>
/// The tab ids, the addresses that point at a tab and the time-range clamp (UX plan D12, E-5).
/// </summary>
public class PerformanceDashboardTabsTests
{
    [Theory]
    [InlineData(-100, 24)]
    [InlineData(-1, 24)]
    [InlineData(0, 24)]
    [InlineData(1, 24)]
    [InlineData(24, 24)]
    [InlineData(25, 168)]
    [InlineData(48, 168)]
    [InlineData(168, 168)]
    [InlineData(169, 720)]
    [InlineData(500, 720)]
    [InlineData(720, 720)]
    [InlineData(100000, 720)]
    [InlineData(int.MaxValue, 720)]
    [InlineData(int.MinValue, 24)]
    public void NormalizeHours_ClampsToASupportedRange(int requested, int expected)
    {
        PerformanceDashboardTabs.NormalizeHours(requested).Should().Be(expected);
    }

    [Theory]
    [InlineData("overview", "overview")]
    [InlineData("Health", "health")]
    [InlineData(" ALERTS ", "alerts")]
    [InlineData("api", "api")]
    [InlineData("bogus", "overview")]
    [InlineData("", "overview")]
    [InlineData(null, "overview")]
    public void NormalizeTab_AcceptsKnownTabsAndFallsBackToOverview(string? requested, string expected)
    {
        PerformanceDashboardTabs.NormalizeTab(requested).Should().Be(expected);
    }

    [Fact]
    public void TabUrl_PointsAtTheShellWithTheTabInTheQueryString()
    {
        PerformanceDashboardTabs.TabUrl(PerformanceDashboardTabs.Alerts).Should().Be("/Admin/Performance?tab=alerts");
    }

    [Fact]
    public void TabUrl_OmitsTheDefaultRangeAndClampsOthers()
    {
        PerformanceDashboardTabs.TabUrl("commands", 24).Should().Be("/Admin/Performance?tab=commands");
        PerformanceDashboardTabs.TabUrl("commands", 168).Should().Be("/Admin/Performance?tab=commands&hours=168");
        PerformanceDashboardTabs.TabUrl("commands", 99999).Should().Be("/Admin/Performance?tab=commands&hours=720");
        PerformanceDashboardTabs.TabUrl("commands", -5).Should().Be("/Admin/Performance?tab=commands");
    }

    [Fact]
    public void TabUrl_FallsBackToOverviewForAnUnknownTab()
    {
        PerformanceDashboardTabs.TabUrl("nope").Should().Be("/Admin/Performance?tab=overview");
    }

    [Fact]
    public void EveryTabIdIsKnown()
    {
        foreach (var id in PerformanceDashboardTabs.TabIds)
        {
            PerformanceDashboardTabs.IsKnownTab(id).Should().BeTrue();
        }

        PerformanceDashboardTabs.TabIds.Should().HaveCount(6);
    }
}
