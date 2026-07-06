using DiscordBot.Bot.Blazor.Shared;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Shared;

public class FilterableTableTests : TestContext
{
    private static RenderFragment Fragment(string text) => b => b.AddContent(0, text);

    private static RenderFragment Row(string item) => b =>
    {
        b.OpenElement(0, "tr");
        b.AddContent(1, item);
        b.CloseElement();
    };

    private IRenderedComponent<FilterableTable<string>> Render(Action<ComponentParameterCollectionBuilder<FilterableTable<string>>>? extra = null, params string[] items) =>
        RenderComponent<FilterableTable<string>>(p =>
        {
            p.Add(x => x.Items, items)
                .Add(x => x.HeaderTemplate, Row("header"))
                .Add(x => x.RowTemplate, (string i) => Row($"row:{i}"))
                .Add(x => x.MobileTemplate, (string i) => Fragment($"card:{i}"));
            extra?.Invoke(p);
        });

    [Fact]
    public void Loading_ShowsSpinnerOnly()
    {
        var cut = Render(p => p.Add(x => x.Loading, true), "a");

        cut.Find(".animate-spin").Should().NotBeNull();
        cut.Markup.Should().NotContain("row:a");
    }

    [Fact]
    public void RendersRows_MobileCards_AndSummary()
    {
        var cut = Render(p => p
            .Add(x => x.TotalCount, 60)
            .Add(x => x.CurrentPage, 2)
            .Add(x => x.PageSize, 25)
            .Add(x => x.ItemNoun, "members"), "a", "b");

        cut.Markup.Should().Contain("row:a").And.Contain("row:b");
        cut.Markup.Should().Contain("card:a").And.Contain("card:b");
        // Page 2 of 60 at 25/page → "Showing 26 to 50 of 60 members"
        var summary = cut.FindAll("div").First(d => d.TextContent.Contains("Showing"));
        summary.TextContent.Should().Contain("26").And.Contain("50").And.Contain("60").And.Contain("members");
    }

    [Fact]
    public void EmptyItems_ShowsEmptyState()
    {
        var cut = Render(p => p
            .Add(x => x.EmptyTitle, "No members found")
            .Add(x => x.EmptyDescription, "Adjust your filters."));

        cut.Markup.Should().Contain("No members found").And.Contain("Adjust your filters.");
        cut.Markup.Should().NotContain("<table");
    }

    [Fact]
    public void PagerClick_RaisesOnPageChange()
    {
        var requested = 0;
        var cut = Render(p => p
            .Add(x => x.CurrentPage, 1)
            .Add(x => x.TotalPages, 3)
            .Add(x => x.TotalCount, 75)
            .Add(x => x.OnPageChange, page => requested = page), "a");

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "3").Click();

        requested.Should().Be(3);
    }

    [Fact]
    public void FiltersAndToolbar_RenderWhenProvided()
    {
        var cut = Render(p => p
            .Add(x => x.Filters, Fragment("the-filters"))
            .Add(x => x.Toolbar, Fragment("the-toolbar")), "a");

        cut.Markup.Should().Contain("the-filters").And.Contain("the-toolbar");
    }
}
