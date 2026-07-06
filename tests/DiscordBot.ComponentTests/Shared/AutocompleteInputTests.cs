using DiscordBot.Bot.Blazor.Shared;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class AutocompleteInputTests : TestContext
{
    private static IRenderedComponent<AutocompleteInput<string>> Render(
        TestContext ctx,
        Func<string, Task<IReadOnlyList<string>>> searchFunc,
        Action<string>? onSelect = null)
        => ctx.RenderComponent<AutocompleteInput<string>>(p => p
            .Add(x => x.Id, "user-id")
            .Add(x => x.Name, "UserId")
            .Add(x => x.MinChars, 2)
            .Add(x => x.DebounceMs, 50)
            .Add(x => x.SearchFunc, searchFunc)
            .Add(x => x.OnSelect, onSelect ?? (_ => { })));

    [Fact]
    public async Task Typing_TriggersSearchFunc_AfterDebounce_AndShowsResults()
    {
        var searchedTerms = new List<string>();
        var cut = Render(this, term =>
        {
            searchedTerms.Add(term);
            return Task.FromResult<IReadOnlyList<string>>(new[] { "alpha", "alphonse" });
        });

        cut.Find("input[type=text]").Input("al");

        searchedTerms.Should().BeEmpty("search must not fire before the debounce window elapses");

        await Task.Delay(300);
        cut.WaitForAssertion(() =>
        {
            searchedTerms.Should().Equal("al");
            cut.FindAll("[role=option]").Should().HaveCount(2);
        });
    }

    [Fact]
    public async Task RapidTyping_CoalescesIntoSingleSearch()
    {
        var searchedTerms = new List<string>();
        var cut = Render(this, term =>
        {
            searchedTerms.Add(term);
            return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        });

        var input = cut.Find("input[type=text]");
        input.Input("al");
        input.Input("alp");
        input.Input("alph");

        await Task.Delay(300);
        cut.WaitForAssertion(() => searchedTerms.Should().Equal("alph"));
    }

    [Fact]
    public async Task ClickingResult_RaisesOnSelect_AndClosesDropdown()
    {
        string? selected = null;
        var cut = Render(
            this,
            _ => Task.FromResult<IReadOnlyList<string>>(new[] { "alpha", "beta" }),
            item => selected = item);

        cut.Find("input[type=text]").Input("al");
        await Task.Delay(300);
        cut.WaitForAssertion(() => cut.FindAll("[role=option]").Should().HaveCount(2));

        cut.FindAll("[role=option]").ElementAt(1).MouseDown();

        selected.Should().Be("beta");
        cut.FindAll("[role=option]").Should().BeEmpty();
        cut.Find("input[type=hidden]").GetAttribute("value").Should().Be("beta");
    }

    [Fact]
    public async Task Escape_ClosesDropdown()
    {
        var cut = Render(this, _ => Task.FromResult<IReadOnlyList<string>>(new[] { "alpha" }));

        cut.Find("input[type=text]").Input("al");
        await Task.Delay(300);
        cut.WaitForAssertion(() => cut.FindAll("[role=option]").Should().HaveCount(1));

        cut.Find("input[type=text]").KeyDown("Escape");

        cut.FindAll("[role=option]").Should().BeEmpty();
    }
}
