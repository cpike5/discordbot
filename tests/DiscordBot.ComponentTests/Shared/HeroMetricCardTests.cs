using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class HeroMetricCardTests : TestContext
{
    [Fact]
    public void RendersTitleValue_AndAccentClass()
    {
        var cut = RenderComponent<HeroMetricCard>(p => p
            .Add(x => x.Model, new HeroMetricCardViewModel
            {
                Title = "Total Servers",
                Value = "12",
                AccentColor = CardAccent.Success
            }));

        cut.Find("div.hero-metric-card").ClassList.Should().Contain("accent-success");
        cut.Markup.Should().Contain("Total Servers");
        cut.Find("p.text-3xl").TextContent.Should().Be("12");
    }

    [Fact]
    public void RendersTrend_WithDirectionClass()
    {
        var cut = RenderComponent<HeroMetricCard>(p => p
            .Add(x => x.Model, new HeroMetricCardViewModel
            {
                Title = "Users",
                Value = "1,847",
                TrendValue = "+124",
                TrendDirection = TrendDirection.Up,
                TrendLabel = "this week"
            }));

        cut.Find("span.trend-up").TextContent.Should().Contain("+124");
        cut.Markup.Should().Contain("this week");
    }

    [Fact]
    public void RendersSparklineBars_FromData()
    {
        var cut = RenderComponent<HeroMetricCard>(p => p
            .Add(x => x.Model, new HeroMetricCardViewModel
            {
                Title = "Commands",
                Value = "3.2K",
                ShowSparkline = true,
                SparklineData = new List<int> { 40, 65, 100 }
            }));

        var bars = cut.FindAll("div.sparkline-bar").ToList();
        bars.Should().HaveCount(3);
        bars.Last().GetAttribute("style").Should().Contain("height: 100%");
    }

    [Fact]
    public void AddsDataAttribute_ToValueElement()
    {
        var cut = RenderComponent<HeroMetricCard>(p => p
            .Add(x => x.Model, new HeroMetricCardViewModel
            {
                Title = "Commands",
                Value = "5",
                DataAttribute = "data-total-commands"
            }));

        cut.Find("p.text-3xl").HasAttribute("data-total-commands").Should().BeTrue();
    }
}
