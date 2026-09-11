using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Overlays;

public class UserPreviewPopoverContentTests : BlazorComponentTestContext
{
    private static readonly UserPreviewViewModel Model = new()
    {
        UserId = 1,
        Username = "exampleuser",
        ProfileUrl = "/profile"
    };

    [Fact]
    public void RendersUsername()
    {
        var cut = Render<UserPreviewPopoverContent>(p => p.Add(x => x.Model, Model));
        cut.Find(".preview-username").TextContent.Should().Be("exampleuser");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<UserPreviewPopoverContent>(p => p
            .Add(x => x.Model, Model)
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "user-preview"));

        var root = cut.Find(".preview-popup-user");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("user-preview");
    }
}
