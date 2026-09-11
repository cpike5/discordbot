using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Forms;

public class AutocompleteTests : BlazorComponentTestContext
{
    private static readonly IReadOnlyList<AutocompleteItem> Items = new List<AutocompleteItem>
    {
        new("1", "Alice", "Moderator"),
        new("2", "Alan", "Member"),
        new("3", "Bob", "Member")
    };

    private static Func<string, CancellationToken, Task<IReadOnlyList<AutocompleteItem>>> FakeSearch(int callCount = 0) => (term, ct) =>
        Task.FromResult<IReadOnlyList<AutocompleteItem>>(Items.Where(i => i.Text.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList());

    [Fact]
    public void MinChars_NotReached_DoesNotOpenDropdown()
    {
        var cut = Render<Autocomplete>(p => p.Add(x => x.Id, "ac").Add(x => x.SearchFunc, FakeSearch()).Add(x => x.MinChars, 2));

        cut.Find("input").Input("a");

        cut.Find("input").GetAttribute("aria-expanded").Should().Be("false");
    }

    [Fact]
    public async Task Typing_AboveMinChars_DebouncesThenSearches_AndOpensWithResults()
    {
        var cut = Render<Autocomplete>(p => p
            .Add(x => x.Id, "ac")
            .Add(x => x.SearchFunc, FakeSearch())
            .Add(x => x.MinChars, 2)
            .Add(x => x.DebounceMs, 1));

        cut.Find("input").Input("al");

        cut.WaitForAssertion(() => cut.Find("input").GetAttribute("aria-expanded").Should().Be("true"), TimeSpan.FromSeconds(2));

        var items = cut.FindAll(".autocomplete-item");
        items.Should().HaveCount(2); // Alice, Alan
    }

    [Fact]
    public async Task NoResults_ShowsNoResultsMessage()
    {
        var cut = Render<Autocomplete>(p => p
            .Add(x => x.Id, "ac")
            .Add(x => x.SearchFunc, FakeSearch())
            .Add(x => x.DebounceMs, 1)
            .Add(x => x.NoResultsMessage, "Nothing here"));

        cut.Find("input").Input("zzz");

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Nothing here"), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ArrowDown_ThenEnter_SelectsHighlightedItem_AndInvokesValueChanged()
    {
        string? captured = null;
        var cut = Render<Autocomplete>(p => p
            .Add(x => x.Id, "ac")
            .Add(x => x.SearchFunc, FakeSearch())
            .Add(x => x.DebounceMs, 1)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => captured = v)));

        cut.Find("input").Input("al");
        cut.WaitForAssertion(() => cut.FindAll(".autocomplete-item").Should().HaveCount(2), TimeSpan.FromSeconds(2));

        cut.Find("input").KeyDown("ArrowDown");
        cut.Find("input").KeyDown("Enter");

        captured.Should().Be("1"); // Alice is first result
        cut.Find("input").GetAttribute("value").Should().Be("Alice");
    }

    [Fact]
    public async Task Escape_ClosesDropdown()
    {
        var cut = Render<Autocomplete>(p => p.Add(x => x.Id, "ac").Add(x => x.SearchFunc, FakeSearch()).Add(x => x.DebounceMs, 1));

        cut.Find("input").Input("al");
        cut.WaitForAssertion(() => cut.Find("input").GetAttribute("aria-expanded").Should().Be("true"), TimeSpan.FromSeconds(2));

        cut.Find("input").KeyDown("Escape");

        cut.Find("input").GetAttribute("aria-expanded").Should().Be("false");
    }

    [Fact]
    public async Task ClickingResult_SelectsIt_ViaMouseDown()
    {
        string? captured = null;
        var cut = Render<Autocomplete>(p => p
            .Add(x => x.Id, "ac")
            .Add(x => x.SearchFunc, FakeSearch())
            .Add(x => x.DebounceMs, 1)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => captured = v)));

        cut.Find("input").Input("bob");
        cut.WaitForAssertion(() => cut.FindAll(".autocomplete-item").Should().HaveCount(1), TimeSpan.FromSeconds(2));

        cut.Find(".autocomplete-item").MouseDown();

        captured.Should().Be("3");
    }

    [Fact]
    public void ClearButton_OnlyRendersWhenValueIsSet()
    {
        var cut = Render<Autocomplete>(p => p.Add(x => x.Id, "ac").Add(x => x.SearchFunc, FakeSearch()));
        cut.FindAll(".autocomplete-clear").Should().BeEmpty();

        var cutWithValue = Render<Autocomplete>(p => p.Add(x => x.Id, "ac").Add(x => x.SearchFunc, FakeSearch()).Add(x => x.Value, "1"));
        cutWithValue.FindAll(".autocomplete-clear").Should().HaveCount(1);
    }

    [Fact]
    public async Task ClearButton_ClearsValueAndDisplayText()
    {
        string? captured = "unset";
        var cut = Render<Autocomplete>(p => p
            .Add(x => x.Id, "ac")
            .Add(x => x.SearchFunc, FakeSearch())
            .Add(x => x.Value, "1")
            .Add(x => x.DisplayText, "Alice")
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<string?>(this, v => captured = v)));

        cut.Find(".autocomplete-clear").Click();

        captured.Should().BeNull();
        cut.Find("input").GetAttribute("value").Should().BeEmpty();
    }
}
