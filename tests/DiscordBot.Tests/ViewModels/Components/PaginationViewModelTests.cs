using DiscordBot.Bot.ViewModels.Components;
using FluentAssertions;

namespace DiscordBot.Tests.ViewModels.Components;

/// <summary>
/// The pagination rules for empty, single-page, out-of-range and unknown-total states
/// (UX plan Phase 3): the partial draws whatever these say.
/// </summary>
public class PaginationViewModelTests
{
    [Fact]
    public void NoItems_IsEmpty_AndHasNoRange()
    {
        var model = new PaginationViewModel { CurrentPage = 1, TotalPages = 0, TotalItems = 0, PageSize = 25 };

        model.IsEmpty.Should().BeTrue();
        model.HasMultiplePages.Should().BeFalse();
        model.FirstItem.Should().Be(0);
        model.LastItem.Should().Be(0, "an empty list must never read \"Showing 1-0 of 0\"");
    }

    [Fact]
    public void SinglePage_ShowsTheWholeRange_AndNothingToNavigate()
    {
        var model = new PaginationViewModel { CurrentPage = 1, TotalPages = 1, TotalItems = 7, PageSize = 25 };

        model.IsEmpty.Should().BeFalse();
        model.HasMultiplePages.Should().BeFalse();
        model.FirstItem.Should().Be(1);
        model.LastItem.Should().Be(7);
        model.IsFirstPage.Should().BeTrue();
        model.IsLastPage.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, 1, 25)]
    [InlineData(2, 26, 50)]
    [InlineData(4, 76, 90)]
    public void Range_EndsAtTheTotal_OnTheLastPage(int page, int first, int last)
    {
        var model = new PaginationViewModel { CurrentPage = page, TotalPages = 4, TotalItems = 90, PageSize = 25 };

        model.FirstItem.Should().Be(first);
        model.LastItem.Should().Be(last);
    }

    [Fact]
    public void PagePastTheEnd_IsClampedToTheLastPage_NotAnInvertedRange()
    {
        var model = new PaginationViewModel { CurrentPage = 9, TotalPages = 4, TotalItems = 90, PageSize = 25 };

        model.EffectivePage.Should().Be(4);
        model.FirstItem.Should().Be(76);
        model.LastItem.Should().Be(90);
        model.FirstItem.Should().BeLessThanOrEqualTo(model.LastItem, "never \"Showing 201 to 90\"");
        model.IsLastPage.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void PageBelowOne_IsClampedToTheFirstPage(int page)
    {
        var model = new PaginationViewModel { CurrentPage = page, TotalPages = 4, TotalItems = 90, PageSize = 25 };

        model.EffectivePage.Should().Be(1);
        model.IsFirstPage.Should().BeTrue();
        model.FirstItem.Should().Be(1);
    }

    [Fact]
    public void UnknownTotal_WithSeveralPages_IsNotEmpty_AndHasNoItemRange()
    {
        var model = new PaginationViewModel { CurrentPage = 2, TotalPages = 6 };

        model.IsEmpty.Should().BeFalse("several pages exist even though the caller did not count the items");
        model.HasKnownTotal.Should().BeFalse();
        model.HasMultiplePages.Should().BeTrue();
        model.FirstItem.Should().Be(0);
        model.LastItem.Should().Be(0);
    }

    [Fact]
    public void ZeroPageSize_DoesNotDivideOrLoop()
    {
        var model = new PaginationViewModel { CurrentPage = 1, TotalPages = 1, TotalItems = 3, PageSize = 0 };

        model.FirstItem.Should().Be(1);
        model.LastItem.Should().Be(1);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    [InlineData(7, 7)]
    public void UpToSevenPages_ShowEveryNumber(int totalPages, int expectedCount)
    {
        var model = new PaginationViewModel { CurrentPage = 1, TotalPages = totalPages };

        model.VisiblePages().Should().HaveCount(expectedCount).And.OnlyContain(p => p.HasValue);
    }

    [Fact]
    public void ManyPages_NearTheStart_ShowsALeadingRunThenEllipsisThenTheLast()
    {
        var model = new PaginationViewModel { CurrentPage = 2, TotalPages = 12 };

        model.VisiblePages().Should().Equal(1, 2, 3, 4, 5, null, 12);
    }

    [Fact]
    public void ManyPages_InTheMiddle_ShowsAWindowBetweenTwoEllipses()
    {
        var model = new PaginationViewModel { CurrentPage = 6, TotalPages = 12 };

        model.VisiblePages().Should().Equal(1, null, 5, 6, 7, null, 12);
    }

    [Fact]
    public void ManyPages_NearTheEnd_ShowsEllipsisThenATrailingRun()
    {
        var model = new PaginationViewModel { CurrentPage = 11, TotalPages = 12 };

        model.VisiblePages().Should().Equal(1, null, 8, 9, 10, 11, 12);
    }

    [Fact]
    public void VisiblePages_UsesTheClampedPage()
    {
        var model = new PaginationViewModel { CurrentPage = 99, TotalPages = 12 };

        model.VisiblePages().Should().Equal(1, null, 8, 9, 10, 11, 12);
    }

    [Fact]
    public void PageSizeSelector_IsHidden_WhenTheListFitsTheSmallestSize()
    {
        var small = new PaginationViewModel
        {
            CurrentPage = 1, TotalPages = 1, TotalItems = 7, PageSize = 25, ShowPageSizeSelector = true
        };
        var empty = new PaginationViewModel { TotalPages = 0, ShowPageSizeSelector = true };
        var larger = new PaginationViewModel
        {
            CurrentPage = 1, TotalPages = 1, TotalItems = 40, PageSize = 50, ShowPageSizeSelector = true
        };
        var disabled = new PaginationViewModel { CurrentPage = 1, TotalPages = 5, TotalItems = 100, PageSize = 25 };

        small.ShowsPageSizeSelector.Should().BeFalse();
        empty.ShowsPageSizeSelector.Should().BeFalse();
        larger.ShowsPageSizeSelector.Should().BeTrue("40 items would split at the smallest page size of 10");
        disabled.ShowsPageSizeSelector.Should().BeFalse("the caller did not ask for it");
    }
}
