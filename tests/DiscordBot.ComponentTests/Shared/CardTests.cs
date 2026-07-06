using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class CardTests : TestContext
{
    [Fact]
    public void RendersTitleSubtitle_AndBodyContent()
    {
        var cut = RenderComponent<Card>(p => p
            .Add(x => x.Model, new CardViewModel
            {
                Title = "My Card",
                Subtitle = "Sub text",
                BodyContent = "<span id=\"body\">Hello</span>"
            }));

        cut.Find("h3").TextContent.Should().Be("My Card");
        cut.Markup.Should().Contain("Sub text");
        cut.Find("#body").TextContent.Should().Be("Hello");
    }

    [Fact]
    public void RendersChildContent_InsteadOfBodyContentString()
    {
        var cut = RenderComponent<Card>(p => p
            .Add(x => x.Model, new CardViewModel { BodyContent = "<span>ignored</span>" })
            .AddChildContent("<em id=\"slot\">slot body</em>"));

        cut.Find("#slot").TextContent.Should().Be("slot body");
        cut.Markup.Should().NotContain("ignored");
    }

    [Fact]
    public void CollapsibleCard_TogglesBodyVisibility()
    {
        var cut = RenderComponent<Card>(p => p
            .Add(x => x.Model, new CardViewModel
            {
                Title = "Collapsible",
                BodyContent = "<p>content</p>",
                IsCollapsible = true,
                IsExpanded = true
            }));

        cut.FindAll("div.p-6.hidden").Should().BeEmpty();

        cut.Find("button").Click();

        cut.FindAll("div.p-6.hidden").Should().HaveCount(1);
    }

    [Fact]
    public void RaisesOnClick_WhenRootClicked()
    {
        var clicked = false;
        var cut = RenderComponent<Card>(p => p
            .Add(x => x.Model, new CardViewModel { BodyContent = "<p>x</p>", IsInteractive = true })
            .Add(x => x.OnClick, () => clicked = true));

        cut.Find("div").Click();

        clicked.Should().BeTrue();
    }
}
