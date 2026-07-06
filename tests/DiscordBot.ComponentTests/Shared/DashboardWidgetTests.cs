using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class DashboardWidgetTests : TestContext
{
    [Fact]
    public void RendersTitle_DetailLink_AndBodyContent()
    {
        var cut = RenderComponent<DashboardWidget>(p => p
            .Add(x => x.Model, new DashboardWidgetViewModel
            {
                Title = "Recent Activity",
                DetailUrl = "/activity",
                BodyContent = "<span id=\"body\">rows</span>"
            }));

        cut.Find("h3").TextContent.Should().Be("Recent Activity");
        cut.Find("a").GetAttribute("href").Should().Be("/activity");
        cut.Find("#body").TextContent.Should().Be("rows");
    }

    [Fact]
    public void RendersEnabledBadge_WhenIsEnabledTrue()
    {
        var cut = RenderComponent<DashboardWidget>(p => p
            .Add(x => x.Model, new DashboardWidgetViewModel
            {
                Title = "Widget",
                IsEnabled = true,
                EnabledLabel = "On"
            }));

        var badge = cut.Find("span.bg-success\\/20");
        badge.TextContent.Trim().Should().Be("On");
    }

    [Fact]
    public void RendersNestedEmptyState_WhenNoBody()
    {
        var cut = RenderComponent<DashboardWidget>(p => p
            .Add(x => x.Model, new DashboardWidgetViewModel
            {
                Title = "Widget",
                EmptyState = new EmptyStateViewModel { Title = "No data yet", Description = "Come back later." }
            }));

        cut.Markup.Should().Contain("No data yet");
        cut.Markup.Should().Contain("Come back later.");
    }

    [Fact]
    public void UsesColSpanTwoClass_WhenColSpanIsTwo()
    {
        var cut = RenderComponent<DashboardWidget>(p => p
            .Add(x => x.Model, new DashboardWidgetViewModel { Title = "Wide", ColSpan = 2 }));

        cut.Find("div").ClassList.Should().Contain("md:col-span-2");
    }
}
