using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Navigation;

public class PageHeaderTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersTitle()
    {
        var cut = Render<PageHeader>(p => p.Add(x => x.Title, "Commands"));
        cut.Find("h1.page-title").TextContent.Should().Be("Commands");
    }

    [Fact]
    public void Subtitle_Renders_WithLegacyDataAttribute()
    {
        var cut = Render<PageHeader>(p => p.Add(x => x.Title, "Commands").Add(x => x.Subtitle, "Every registered command"));
        var subtitle = cut.Find("p[data-command-subtitle]");
        subtitle.TextContent.Should().Be("Every registered command");
    }

    [Fact]
    public void NoSubtitle_RendersNoSubtitleParagraph()
    {
        var cut = Render<PageHeader>(p => p.Add(x => x.Title, "Commands"));
        cut.FindAll("p").Should().BeEmpty();
    }

    [Fact]
    public void NoActions_RendersNoActionsWrapper()
    {
        var cut = Render<PageHeader>(p => p.Add(x => x.Title, "Commands"));
        cut.Markup.Should().NotContain("justify-between");
    }

    [Fact]
    public void Actions_RenderInFlexWrapper()
    {
        var cut = Render<PageHeader>(p => p
            .Add(x => x.Title, "Audit Logs")
            .Add(x => x.Actions, (Microsoft.AspNetCore.Components.RenderFragment)(b => b.AddMarkupContent(0, "<button data-testid='action'>Export</button>"))));

        cut.Find("[data-testid='action']").TextContent.Should().Be("Export");
        cut.Markup.Should().Contain("justify-between");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<PageHeader>(p => p
            .Add(x => x.Title, "x")
            .Add(x => x.Class, "extra")
            .AddUnmatched("data-testid", "header"));

        var root = cut.Find("div");
        root.ClassList.Should().Contain("extra");
        root.GetAttribute("data-testid").Should().Be("header");
    }
}
