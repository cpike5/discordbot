using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class EnhancedCardTests : TestContext
{
    [Fact]
    public void RendersAccentClass_AndId()
    {
        var cut = RenderComponent<EnhancedCard>(p => p
            .Add(x => x.Model, new EnhancedCardViewModel
            {
                Id = "my-card",
                Title = "Enhanced",
                BodyContent = "<p>body</p>",
                AccentColor = CardAccent.Orange
            }));

        var root = cut.Find("#my-card");
        root.ClassList.Should().Contain("card-enhanced");
        root.ClassList.Should().Contain("accent-orange");
        cut.Find("h3").TextContent.Should().Be("Enhanced");
    }

    [Fact]
    public void UsesCompactPadding_WhenRequested()
    {
        var cut = RenderComponent<EnhancedCard>(p => p
            .Add(x => x.Model, new EnhancedCardViewModel
            {
                BodyContent = "<p>body</p>",
                CompactPadding = true
            }));

        cut.FindAll("div.p-4").Should().NotBeEmpty();
        cut.FindAll("div.p-6").Should().BeEmpty();
    }

    [Fact]
    public void CollapsibleCard_TogglesAriaExpanded_AndBody()
    {
        var cut = RenderComponent<EnhancedCard>(p => p
            .Add(x => x.Model, new EnhancedCardViewModel
            {
                Title = "Collapsible",
                BodyContent = "<p>content</p>",
                IsCollapsible = true,
                IsExpanded = true
            }));

        var button = cut.Find("button");
        button.GetAttribute("aria-expanded").Should().Be("true");

        button.Click();

        cut.Find("button").GetAttribute("aria-expanded").Should().Be("false");
        cut.FindAll("div.hidden").Should().HaveCount(1);
    }

    [Fact]
    public void RendersFooter_WhenFooterContentProvided()
    {
        var cut = RenderComponent<EnhancedCard>(p => p
            .Add(x => x.Model, new EnhancedCardViewModel
            {
                BodyContent = "<p>body</p>",
                FooterContent = "<span id=\"foot\">footer here</span>"
            }));

        cut.Find("#foot").TextContent.Should().Be("footer here");
        cut.Markup.Should().Contain("border-t border-border-primary");
    }
}
