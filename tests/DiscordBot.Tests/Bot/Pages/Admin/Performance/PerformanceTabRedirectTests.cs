using DiscordBot.Bot.Pages.Admin.Performance;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace DiscordBot.Tests.Bot.Pages.Admin.Performance;

/// <summary>
/// The five standalone Performance pages are retired (UX plan D12). Their routes stay and answer
/// with a permanent redirect to the matching tab of the dashboard shell, so old links, bookmarks
/// and stored notification links keep working.
/// </summary>
public class PerformanceTabRedirectTests
{
    [Fact]
    public void SystemHealth_RedirectsToTheSystemTab()
    {
        AssertRedirect(new SystemHealthModel().OnGet(), "/Admin/Performance?tab=system");
    }

    [Fact]
    public void ApiMetrics_RedirectsToTheApiTab()
    {
        AssertRedirect(new ApiMetricsModel().OnGet(), "/Admin/Performance?tab=api");
    }

    [Fact]
    public void HealthMetrics_RedirectsToTheHealthTab()
    {
        AssertRedirect(new HealthMetricsModel().OnGet(), "/Admin/Performance?tab=health");
    }

    [Fact]
    public void Alerts_RedirectsToTheAlertsTab()
    {
        AssertRedirect(new AlertsModel().OnGet(), "/Admin/Performance?tab=alerts");
    }

    [Fact]
    public void Commands_RedirectsToTheCommandsTab()
    {
        AssertRedirect(new CommandsModel().OnGet(), "/Admin/Performance?tab=commands");
    }

    [Theory]
    [InlineData(168, "/Admin/Performance?tab=commands&hours=168")]
    [InlineData(720, "/Admin/Performance?tab=commands&hours=720")]
    [InlineData(24, "/Admin/Performance?tab=commands")]
    public void Commands_ForwardsASupportedRange(int hours, string expected)
    {
        AssertRedirect(new CommandsModel { Hours = hours }.OnGet(), expected);
    }

    [Theory]
    [InlineData(100000, "/Admin/Performance?tab=api&hours=720")]
    [InlineData(-5, "/Admin/Performance?tab=api")]
    [InlineData(0, "/Admin/Performance?tab=api")]
    [InlineData(48, "/Admin/Performance?tab=api&hours=168")]
    public void ApiMetrics_ClampsAnOutOfRangeHoursBeforeForwardingIt(int hours, string expected)
    {
        AssertRedirect(new ApiMetricsModel { Hours = hours }.OnGet(), expected);
    }

    private static void AssertRedirect(IActionResult result, string expectedUrl)
    {
        var redirect = result.Should().BeOfType<RedirectResult>().Subject;
        redirect.Permanent.Should().BeTrue("the old routes moved for good");
        redirect.Url.Should().Be(expectedUrl);
    }
}
