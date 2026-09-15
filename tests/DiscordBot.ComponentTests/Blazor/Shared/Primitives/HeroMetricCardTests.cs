using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class HeroMetricCardTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersTitleAndValue()
    {
        var cut = Render<HeroMetricCard>(p => p.Add(x => x.Title, "Total Servers").Add(x => x.Value, "12"));

        cut.Find("p.hero-metric-label").TextContent.Should().Be("Total Servers");
        cut.Find("p.hero-metric-value").TextContent.Should().Be("12");
    }

    [Theory]
    [InlineData(CardAccent.Blue, "accent-blue")]
    [InlineData(CardAccent.Orange, "accent-orange")]
    [InlineData(CardAccent.Success, "accent-success")]
    [InlineData(CardAccent.Info, "accent-info")]
    public void AccentColor_AppliesExpectedClass(CardAccent accent, string expectedClass)
    {
        var cut = Render<HeroMetricCard>(p => p.Add(x => x.Title, "x").Add(x => x.Value, "1").Add(x => x.AccentColor, accent));
        cut.Find("div.hero-metric-card").ClassList.Should().Contain(expectedClass);
    }

    [Theory]
    [InlineData(TrendDirection.Up, "trend-up")]
    [InlineData(TrendDirection.Down, "trend-down")]
    [InlineData(TrendDirection.Neutral, "trend-neutral")]
    public void TrendDirection_AppliesExpectedClass_WhenTrendValueSet(TrendDirection direction, string expectedClass)
    {
        var cut = Render<HeroMetricCard>(p => p
            .Add(x => x.Title, "x").Add(x => x.Value, "1")
            .Add(x => x.TrendValue, "+2").Add(x => x.TrendDirection, direction));

        cut.Find($"span.{expectedClass}").Should().NotBeNull();
    }

    [Fact]
    public void NoTrendValueOrLabel_DoesNotRenderTrendRow()
    {
        var cut = Render<HeroMetricCard>(p => p.Add(x => x.Title, "x").Add(x => x.Value, "1"));
        cut.FindAll("span.trend-up, span.trend-down, span.trend-neutral").Should().BeEmpty();
    }

    [Fact]
    public void IconPath_RendersThroughIconComponent()
    {
        var cut = Render<HeroMetricCard>(p => p
            .Add(x => x.Title, "x").Add(x => x.Value, "1").Add(x => x.IconPath, IconPaths.CheckCircle));

        var div = cut.Find("div.hero-metric-icon");
        div.QuerySelector("svg path")!.GetAttribute("d").Should().Be(IconPaths.CheckCircle);
    }

    [Fact]
    public void IconContent_TakesPriorityOverIconPath()
    {
        var cut = Render<HeroMetricCard>(p => p
            .Add(x => x.Title, "x").Add(x => x.Value, "1")
            .Add(x => x.IconPath, IconPaths.CheckCircle)
            .Add(x => x.IconContent, (RenderFragment)(b => b.AddMarkupContent(0, "<circle data-testid='custom-icon' />"))));

        cut.Find("[data-testid='custom-icon']").Should().NotBeNull();
    }

    [Fact]
    public void NoIcon_DoesNotRenderIconDiv()
    {
        var cut = Render<HeroMetricCard>(p => p.Add(x => x.Title, "x").Add(x => x.Value, "1"));
        cut.FindAll("div.hero-metric-icon").Should().BeEmpty();
    }

    [Fact]
    public void ShowSparkline_RendersBarsWithHeightStyle()
    {
        var cut = Render<HeroMetricCard>(p => p
            .Add(x => x.Title, "x").Add(x => x.Value, "1")
            .Add(x => x.ShowSparkline, true)
            .Add(x => x.SparklineData, new List<int> { 40, 80 }));

        var bars = cut.FindAll("div.sparkline-bar");
        bars.Should().HaveCount(2);
        bars[0].GetAttribute("style").Should().Contain("height: 40%");
    }

    [Fact]
    public void ShowSparkline_False_DoesNotRenderSparkline()
    {
        var cut = Render<HeroMetricCard>(p => p
            .Add(x => x.Title, "x").Add(x => x.Value, "1")
            .Add(x => x.SparklineData, new List<int> { 40, 80 }));

        cut.FindAll("div.sparkline").Should().BeEmpty();
    }

    [Fact]
    public void DataAttribute_EmitsBareAttributeOnValueElement()
    {
        var cut = Render<HeroMetricCard>(p => p
            .Add(x => x.Title, "x").Add(x => x.Value, "42")
            .Add(x => x.DataAttribute, "data-total-commands"));

        var value = cut.Find("p.hero-metric-value");
        value.HasAttribute("data-total-commands").Should().BeTrue();
    }

    [Fact]
    public void NoDataAttribute_DoesNotEmitOne()
    {
        var cut = Render<HeroMetricCard>(p => p.Add(x => x.Title, "x").Add(x => x.Value, "42"));
        cut.Find("p.hero-metric-value").Attributes.Should().NotContain(a => a.Name.StartsWith("data-"));
    }

    [Fact]
    public void Id_And_Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<HeroMetricCard>(p => p
            .Add(x => x.Title, "x").Add(x => x.Value, "1")
            .Add(x => x.Id, "my-metric").Add(x => x.Class, "extra")
            .AddUnmatched("data-testid", "hero"));

        var root = cut.Find("div#my-metric");
        root.ClassList.Should().Contain("extra");
        root.GetAttribute("data-testid").Should().Be("hero");
    }
}
