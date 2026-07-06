using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class EmptyStateTests : TestContext
{
    [Fact]
    public void RendersTitleAndDescription()
    {
        var cut = RenderComponent<EmptyState>(p => p
            .Add(x => x.Model, new EmptyStateViewModel
            {
                Title = "Nothing here",
                Description = "Add an item to get started."
            }));

        cut.Find("h3").TextContent.Should().Be("Nothing here");
        cut.Find("p").TextContent.Should().Be("Add an item to get started.");
    }

    [Fact]
    public void RendersPrimaryActionAsLink_WhenUrlProvided()
    {
        var cut = RenderComponent<EmptyState>(p => p
            .Add(x => x.Model, new EmptyStateViewModel
            {
                PrimaryActionText = "Create",
                PrimaryActionUrl = "/create"
            }));

        var link = cut.Find("a");
        link.GetAttribute("href").Should().Be("/create");
        link.TextContent.Trim().Should().Be("Create");
        cut.FindAll("button").Should().BeEmpty();
    }

    [Fact]
    public void PrimaryActionButton_RaisesOnPrimaryAction()
    {
        var clicked = false;
        var cut = RenderComponent<EmptyState>(p => p
            .Add(x => x.Model, new EmptyStateViewModel { PrimaryActionText = "Retry" })
            .Add(x => x.OnPrimaryAction, () => clicked = true));

        cut.Find("button").Click();

        clicked.Should().BeTrue();
    }

    [Fact]
    public void AppliesCompactSizeClasses()
    {
        var cut = RenderComponent<EmptyState>(p => p
            .Add(x => x.Model, new EmptyStateViewModel { Size = EmptyStateSize.Compact }));

        cut.Markup.Should().Contain("max-w-[320px]");
        cut.Find("h3").ClassList.Should().Contain("text-base");
    }
}
