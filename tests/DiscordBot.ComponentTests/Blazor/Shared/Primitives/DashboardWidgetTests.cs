using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class DashboardWidgetTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersTitleAndSubtitle()
    {
        var cut = Render<DashboardWidget>(p => p.Add(x => x.Title, "Recent Activity").Add(x => x.Subtitle, "Last 24 hours"));

        cut.Find("h3").TextContent.Should().Be("Recent Activity");
        cut.Markup.Should().Contain("Last 24 hours");
    }

    [Theory]
    [InlineData(1, "md:col-span-1")]
    [InlineData(2, "md:col-span-2")]
    public void ColSpan_AppliesExpectedClass(int colSpan, string expectedClass)
    {
        var cut = Render<DashboardWidget>(p => p.Add(x => x.Title, "x").Add(x => x.ColSpan, colSpan));
        cut.Find("div").ClassList.Should().Contain(expectedClass);
    }

    [Theory]
    [InlineData(true, "Enabled")]
    [InlineData(false, "Disabled")]
    public void IsEnabled_RendersExpectedBadge(bool isEnabled, string expectedLabel)
    {
        var cut = Render<DashboardWidget>(p => p.Add(x => x.Title, "x").Add(x => x.IsEnabled, isEnabled));
        cut.Markup.Should().Contain(expectedLabel);
    }

    [Fact]
    public void IsEnabled_Null_RendersNoBadge()
    {
        var cut = Render<DashboardWidget>(p => p.Add(x => x.Title, "x"));
        cut.Markup.Should().NotContain("Enabled").And.NotContain("Disabled");
    }

    [Fact]
    public void DetailUrl_RendersDetailLink()
    {
        var cut = Render<DashboardWidget>(p => p.Add(x => x.Title, "x").Add(x => x.DetailUrl, "/foo").Add(x => x.DetailLinkText, "See more"));
        var link = cut.Find("a");
        link.GetAttribute("href").Should().Be("/foo");
        link.TextContent.Should().Contain("See more");
    }

    [Fact]
    public void HeaderActions_RenderAsLinks()
    {
        var cut = Render<DashboardWidget>(p => p.Add(x => x.Title, "x").Add(x => x.HeaderActions,
            new List<WidgetHeaderAction> { new() { Text = "Settings", Url = "/settings" } }));

        var link = cut.Find("a[href='/settings']");
        link.TextContent.Should().Contain("Settings");
    }

    [Fact]
    public void ChildContent_TakesPriorityOverEmptyStateAndEmptyContent()
    {
        var cut = Render<DashboardWidget>(p => p
            .Add(x => x.Title, "x")
            .Add(x => x.EmptyState, new EmptyStateViewModel { Title = "Empty" })
            .AddChildContent("<p data-testid='body'>Body</p>"));

        cut.Find("[data-testid='body']").Should().NotBeNull();
        cut.Markup.Should().NotContain("Empty");
    }

    [Fact]
    public void EmptyState_RendersThroughEmptyStateComponent_WhenNoChildContent()
    {
        var cut = Render<DashboardWidget>(p => p
            .Add(x => x.Title, "x")
            .Add(x => x.EmptyState, new EmptyStateViewModel { Title = "No Commands Yet", Description = "Create one." }));

        cut.FindAll("h3").Select(h => h.TextContent).Should().Contain("No Commands Yet");
        cut.Markup.Should().Contain("Create one.");
    }

    [Fact]
    public void EmptyContent_RendersWhenNoChildContent_AndTakesPriorityOverEmptyState()
    {
        var cut = Render<DashboardWidget>(p => p
            .Add(x => x.Title, "x")
            .Add(x => x.EmptyState, new EmptyStateViewModel { Title = "Ignored" })
            .Add(x => x.EmptyContent, (Microsoft.AspNetCore.Components.RenderFragment)(b => b.AddMarkupContent(0, "<p data-testid='custom-empty'>Nothing here</p>"))));

        cut.Find("[data-testid='custom-empty']").Should().NotBeNull();
        cut.Markup.Should().NotContain("Ignored");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<DashboardWidget>(p => p
            .Add(x => x.Title, "x").Add(x => x.Class, "extra").AddUnmatched("data-testid", "widget"));

        var root = cut.Find("div");
        root.ClassList.Should().Contain("extra");
        root.GetAttribute("data-testid").Should().Be("widget");
    }
}
