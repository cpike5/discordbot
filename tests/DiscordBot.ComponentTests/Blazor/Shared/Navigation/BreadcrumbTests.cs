using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Navigation;

public class BreadcrumbTests : BlazorComponentTestContext
{
    private static readonly IReadOnlyList<BreadcrumbItem> ThreeItems = new List<BreadcrumbItem>
    {
        new() { Label = "Home", Url = "/" },
        new() { Label = "Servers", Url = "/servers" },
        new() { Label = "My Server", IsCurrent = true }
    };

    [Fact]
    public void RendersLinksForNonCurrentItems()
    {
        var cut = Render<Breadcrumb>(p => p.Add(x => x.Items, ThreeItems));

        var links = cut.FindAll("a");
        links.Should().HaveCount(2);
        links[0].GetAttribute("href").Should().Be("/");
        links[1].GetAttribute("href").Should().Be("/servers");
    }

    [Fact]
    public void LastItem_RendersAsSpan_WithAriaCurrentPage()
    {
        var cut = Render<Breadcrumb>(p => p.Add(x => x.Items, ThreeItems));

        var spans = cut.FindAll("li span");
        var current = spans.Should().ContainSingle(s => s.GetAttribute("aria-current") == "page").Subject;
        current.TextContent.Should().Be("My Server");
    }

    [Fact]
    public void NonLastItems_DoNotHaveAriaCurrent()
    {
        var cut = Render<Breadcrumb>(p => p.Add(x => x.Items, ThreeItems));
        cut.FindAll("a").Should().OnlyContain(a => a.GetAttribute("aria-current") == null);
    }

    [Fact]
    public void RendersSeparatorBetweenItems_ButNotAfterLast()
    {
        var cut = Render<Breadcrumb>(p => p.Add(x => x.Items, ThreeItems));
        cut.FindAll("svg").Should().HaveCount(2);
    }

    [Fact]
    public void SingleCurrentItem_NoUrl_RendersAsSpan_NoSeparator()
    {
        var items = new List<BreadcrumbItem> { new() { Label = "Only", IsCurrent = true } };
        var cut = Render<Breadcrumb>(p => p.Add(x => x.Items, items));

        cut.FindAll("a").Should().BeEmpty();
        cut.FindAll("svg").Should().BeEmpty();
        cut.Find("span[aria-current='page']").TextContent.Should().Be("Only");
    }

    [Fact]
    public void EmptyItems_RendersNothing()
    {
        var cut = Render<Breadcrumb>(p => p.Add(x => x.Items, Array.Empty<BreadcrumbItem>()));
        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void ItemWithoutUrl_NotCurrent_RendersAsSpan()
    {
        var items = new List<BreadcrumbItem>
        {
            new() { Label = "No link" },
            new() { Label = "Current", IsCurrent = true }
        };
        var cut = Render<Breadcrumb>(p => p.Add(x => x.Items, items));

        cut.FindAll("a").Should().BeEmpty();
        cut.FindAll("li span").Select(s => s.TextContent).Should().Contain("No link");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<Breadcrumb>(p => p
            .Add(x => x.Items, ThreeItems)
            .Add(x => x.Class, "extra")
            .AddUnmatched("data-testid", "crumbs"));

        var nav = cut.Find("nav");
        nav.ClassList.Should().Contain("extra");
        nav.GetAttribute("data-testid").Should().Be("crumbs");
    }
}
