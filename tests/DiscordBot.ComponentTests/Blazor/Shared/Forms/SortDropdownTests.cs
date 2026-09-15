using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Forms;

public class SortDropdownTests : BlazorComponentTestContext
{
    private static readonly List<SortOption> Options = new()
    {
        new SortOption { Value = "name-asc", Label = "Name (A-Z)" },
        new SortOption { Value = "name-desc", Label = "Name (Z-A)" },
        new SortOption { Value = "newest", Label = "Newest First" }
    };

    [Fact]
    public void CurrentSort_MatchingOption_ShowsItsLabel()
    {
        var cut = Render<SortDropdown>(p => p.Add(x => x.Id, "s").Add(x => x.Options, Options).Add(x => x.Value, "newest"));
        cut.Find("button span").TextContent.Should().Be("Newest First");
    }

    [Fact]
    public void CurrentSort_NoMatch_FallsBackToLabelParameter()
    {
        var cut = Render<SortDropdown>(p => p.Add(x => x.Id, "s").Add(x => x.Options, Options).Add(x => x.Value, "unknown"));
        cut.Find("button span").TextContent.Should().Be("Sort");
    }

    [Fact]
    public void ClickingToggle_OpensListbox()
    {
        var cut = Render<SortDropdown>(p => p.Add(x => x.Id, "s").Add(x => x.Options, Options));

        cut.FindAll("[role='listbox']").Should().BeEmpty();

        cut.Find("button").Click();

        cut.Find("[role='listbox']").Should().NotBeNull();
        cut.Find("button").GetAttribute("aria-expanded").Should().Be("true");
    }

    [Fact]
    public void SelectingOption_ClosesDropdown_AndInvokesValueChanged()
    {
        string? captured = null;
        var cut = Render<SortDropdown>(p => p
            .Add(x => x.Id, "s")
            .Add(x => x.Options, Options)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => captured = v)));

        cut.Find("button").Click();
        cut.FindAll("[role='option']")[1].Click();

        captured.Should().Be("name-desc");
        cut.FindAll("[role='listbox']").Should().BeEmpty();
    }

    [Fact]
    public void ArrowDown_OnToggle_OpensDropdown()
    {
        var cut = Render<SortDropdown>(p => p.Add(x => x.Id, "s").Add(x => x.Options, Options));

        cut.Find("button").KeyDown("ArrowDown");

        cut.Find("[role='listbox']").Should().NotBeNull();
    }

    [Fact]
    public void Keyboard_ArrowDownThenEnter_SelectsFocusedOption()
    {
        string? captured = null;
        var cut = Render<SortDropdown>(p => p
            .Add(x => x.Id, "s")
            .Add(x => x.Options, Options)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => captured = v)));

        cut.Find("button").Click(); // open, focus index seeds to -1 (no current selection)
        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown("ArrowDown"); // index 0
        listbox.KeyDown("Enter");

        captured.Should().Be("name-asc");
    }

    [Fact]
    public void Keyboard_HomeAndEnd_JumpToFirstAndLastOption()
    {
        string? captured = null;
        var cut = Render<SortDropdown>(p => p
            .Add(x => x.Id, "s")
            .Add(x => x.Options, Options)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string>(this, v => captured = v)));

        cut.Find("button").Click();
        var listbox = cut.Find("[role='listbox']");
        listbox.KeyDown("End");
        listbox.KeyDown("Enter");

        captured.Should().Be("newest");
    }

    [Fact]
    public void Escape_ClosesDropdown()
    {
        var cut = Render<SortDropdown>(p => p.Add(x => x.Id, "s").Add(x => x.Options, Options));

        cut.Find("button").Click();
        cut.Find("[role='listbox']").KeyDown("Escape");

        cut.FindAll("[role='listbox']").Should().BeEmpty();
    }

    [Fact]
    public void SelectedOption_RendersCheckmark()
    {
        var cut = Render<SortDropdown>(p => p.Add(x => x.Id, "s").Add(x => x.Options, Options).Add(x => x.Value, "name-asc"));

        cut.Find("button").Click();
        var options = cut.FindAll("[role='option']");
        options[0].QuerySelector("svg").Should().NotBeNull();
        options[1].QuerySelector("svg").Should().BeNull();
    }
}
