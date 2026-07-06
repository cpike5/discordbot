using DiscordBot.Bot.Blazor.Shared;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Shared;

public class PaginationTests : TestContext
{
    [Fact]
    public void RendersNothing_WhenSinglePage()
    {
        var cut = RenderComponent<Pagination>(p => p
            .Add(x => x.CurrentPage, 1)
            .Add(x => x.TotalPages, 1));

        cut.Markup.Trim().Should().BeEmpty();
    }

    [Fact]
    public void RendersNumberedWindow_AroundCurrentPage()
    {
        var cut = RenderComponent<Pagination>(p => p
            .Add(x => x.CurrentPage, 5)
            .Add(x => x.TotalPages, 10));

        var labels = cut.FindAll("button")
            .Select(b => b.TextContent.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        labels.Should().Contain(new[] { "3", "4", "5", "6", "7" });
        labels.Should().NotContain("2");
        labels.Should().NotContain("8");
    }

    [Fact]
    public void DisablesPrevOnFirstPage_AndNextOnLastPage()
    {
        var first = RenderComponent<Pagination>(p => p
            .Add(x => x.CurrentPage, 1)
            .Add(x => x.TotalPages, 3));
        first.Find("button[aria-label='Previous page']").HasAttribute("disabled").Should().BeTrue();
        first.Find("button[aria-label='Next page']").HasAttribute("disabled").Should().BeFalse();

        var last = RenderComponent<Pagination>(p => p
            .Add(x => x.CurrentPage, 3)
            .Add(x => x.TotalPages, 3));
        last.Find("button[aria-label='Previous page']").HasAttribute("disabled").Should().BeFalse();
        last.Find("button[aria-label='Next page']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void RaisesOnPageChange_WithClickedPage()
    {
        var requested = 0;
        var cut = RenderComponent<Pagination>(p => p
            .Add(x => x.CurrentPage, 2)
            .Add(x => x.TotalPages, 5)
            .Add(x => x.OnPageChange, page => requested = page));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "4").Click();

        requested.Should().Be(4);
    }

    [Fact]
    public void DoesNotRaiseOnPageChange_ForCurrentPage()
    {
        var raised = false;
        var cut = RenderComponent<Pagination>(p => p
            .Add(x => x.CurrentPage, 2)
            .Add(x => x.TotalPages, 5)
            .Add(x => x.OnPageChange, (int _) => raised = true));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "2").Click();

        raised.Should().BeFalse();
    }
}
