using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Overlays;

public class GuildPreviewPopoverContentTests : BlazorComponentTestContext
{
    private static readonly GuildPreviewViewModel Model = new()
    {
        GuildId = 9,
        Name = "Example Guild",
        DetailsUrl = "#",
        SettingsUrl = "#"
    };

    [Fact]
    public void RendersGuildName()
    {
        var cut = Render<GuildPreviewPopoverContent>(p => p.Add(x => x.Model, Model));
        cut.Find(".preview-username").TextContent.Should().Be("Example Guild");
    }

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<GuildPreviewPopoverContent>(p => p
            .Add(x => x.Model, Model)
            .Add(x => x.Class, "extra-class")
            .AddUnmatched("data-testid", "guild-preview"));

        var root = cut.Find(".preview-popup-guild");
        root.ClassList.Should().Contain("extra-class");
        root.GetAttribute("data-testid").Should().Be("guild-preview");
    }
}
