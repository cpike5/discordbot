using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Forms;

public class FilterPanelTests : BlazorComponentTestContext
{
    [Fact]
    public void RendersTitle_AndChildContent()
    {
        var cut = Render<FilterPanel>(p => p.Add(x => x.Title, "My Filters").AddChildContent("<p>Body</p>"));

        cut.Markup.Should().Contain("My Filters");
        cut.Find("p").TextContent.Should().Be("Body");
    }

    [Fact]
    public void DefaultExpanded_False_StartsCollapsed()
    {
        var cut = Render<FilterPanel>(p => p.Add(x => x.DefaultExpanded, false));

        cut.Find("button").GetAttribute("aria-expanded").Should().Be("false");
        cut.Find("#filterContent").ClassList.Should().Contain("max-h-0");
    }

    [Fact]
    public void DefaultExpanded_True_StartsExpanded()
    {
        var cut = Render<FilterPanel>(p => p.Add(x => x.DefaultExpanded, true));

        cut.Find("button").GetAttribute("aria-expanded").Should().Be("true");
        cut.Find("#filterContent").ClassList.Should().Contain("max-h-screen");
    }

    [Fact]
    public void ClickingHeader_TogglesExpanded_AndInvokesCallback()
    {
        bool? captured = null;
        var cut = Render<FilterPanel>(p => p
            .Add(x => x.DefaultExpanded, false)
            .Add(x => x.IsExpandedChanged, EventCallback.Factory.Create<bool>(this, v => captured = v)));

        cut.Find("button").Click();

        cut.Find("button").GetAttribute("aria-expanded").Should().Be("true");
        captured.Should().BeTrue();

        cut.Find("button").Click();

        cut.Find("button").GetAttribute("aria-expanded").Should().Be("false");
        captured.Should().BeFalse();
    }

    [Fact]
    public void IsCollapsible_False_RendersNoToggleButton()
    {
        var cut = Render<FilterPanel>(p => p.Add(x => x.IsCollapsible, false));
        cut.FindAll("button").Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, "Active")]
    [InlineData(3, "3 active")]
    public void ActiveFilterCount_RendersBadgeWithExpectedText(int count, string expectedText)
    {
        var cut = Render<FilterPanel>(p => p.Add(x => x.ActiveFilterCount, count));
        cut.Markup.Should().Contain(expectedText);
    }

    [Fact]
    public void ActiveFilterCount_Null_RendersNoBadge()
    {
        var cut = Render<FilterPanel>(p => p.Add(x => x.Title, "Filters"));
        cut.Markup.Should().NotContain("bg-accent-orange text-white");
    }

    [Fact]
    public void Id_ControlsContentElementId_ForMultipleInstancesOnOnePage()
    {
        var cut = Render<FilterPanel>(p => p.Add(x => x.Id, "myPanel"));
        cut.Find("#myPanel").Should().NotBeNull();
        cut.Find("button").GetAttribute("aria-controls").Should().Be("myPanel");
    }
}
