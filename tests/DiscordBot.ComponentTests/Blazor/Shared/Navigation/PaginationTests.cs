using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.ComponentTests.Blazor.Shared.Navigation;

public class PaginationTests : BlazorComponentTestContext
{
    // ---- Link mode (BaseUrl set) ---------------------------------------------------------------

    [Fact]
    public void LinkMode_Full_RendersPageNumberLinks_WithCurrentPageHighlighted()
    {
        var cut = Render<Pagination>(p => p
            .Add(x => x.CurrentPage, 3).Add(x => x.TotalPages, 5).Add(x => x.BaseUrl, "/items"));

        cut.Find("span[aria-current='page']").TextContent.Should().Be("3");
        cut.Find("a[href='/items?page=2']").Should().NotBeNull();
        cut.Find("a[href='/items?page=4']").Should().NotBeNull();
    }

    [Fact]
    public void LinkMode_FirstPage_DisablesPreviousAndFirst()
    {
        var cut = Render<Pagination>(p => p
            .Add(x => x.CurrentPage, 1).Add(x => x.TotalPages, 5).Add(x => x.BaseUrl, "/items"));

        cut.Find("a[aria-label='Previous page']").GetAttribute("aria-disabled").Should().Be("true");
        cut.Find("a[aria-label='First page']").GetAttribute("aria-disabled").Should().Be("true");
    }

    [Fact]
    public void LinkMode_ManyPages_RendersEllipsis()
    {
        var cut = Render<Pagination>(p => p
            .Add(x => x.CurrentPage, 5).Add(x => x.TotalPages, 20).Add(x => x.BaseUrl, "/items"));

        cut.Markup.Should().Contain("...");
    }

    [Fact]
    public void LinkMode_ShowFirstLast_False_HidesFirstLastButtons()
    {
        var cut = Render<Pagination>(p => p
            .Add(x => x.CurrentPage, 3).Add(x => x.TotalPages, 5).Add(x => x.BaseUrl, "/items")
            .Add(x => x.ShowFirstLast, false));

        cut.FindAll("a[aria-label='First page']").Should().BeEmpty();
        cut.FindAll("a[aria-label='Last page']").Should().BeEmpty();
    }

    [Fact]
    public void LinkMode_PageNumberLinks_HaveNoAriaDisabled()
    {
        // Page 4's link doesn't coincide with Previous (page 2), Next (page 4 IS Current+1... use
        // page 2 as current so Next targets 3 and Previous targets 1, leaving page-number link "4"
        // unambiguous from any of Previous/Next/First/Last).
        var cut = Render<Pagination>(p => p.Add(x => x.CurrentPage, 2).Add(x => x.TotalPages, 5).Add(x => x.BaseUrl, "/items"));
        cut.Find("a[href='/items?page=4']").HasAttribute("aria-disabled").Should().BeFalse();
    }

    [Theory]
    [InlineData(PaginationStyle.Simple)]
    [InlineData(PaginationStyle.Compact)]
    [InlineData(PaginationStyle.Bordered)]
    [InlineData(PaginationStyle.Full)]
    public void LinkMode_EveryStyle_RendersANav(PaginationStyle style)
    {
        var cut = Render<Pagination>(p => p
            .Add(x => x.CurrentPage, 2).Add(x => x.TotalPages, 5).Add(x => x.Style, style).Add(x => x.BaseUrl, "/items"));

        cut.Find("nav[aria-label='Pagination']").Should().NotBeNull();
    }

    [Fact]
    public void LinkMode_Compact_ShowsPageOfTotal()
    {
        var cut = Render<Pagination>(p => p
            .Add(x => x.CurrentPage, 4).Add(x => x.TotalPages, 8).Add(x => x.Style, PaginationStyle.Compact).Add(x => x.BaseUrl, "/items"));

        cut.Markup.Should().Contain("4").And.Contain("8");
    }

    [Fact]
    public void LinkMode_ShowItemCount_RendersRange()
    {
        var cut = Render<Pagination>(p => p
            .Add(x => x.CurrentPage, 3).Add(x => x.TotalPages, 10).Add(x => x.TotalItems, 247).Add(x => x.PageSize, 25)
            .Add(x => x.ShowItemCount, true).Add(x => x.BaseUrl, "/items"));

        cut.Markup.Should().Contain("51-75").And.Contain("247");
    }

    [Fact]
    public void LinkMode_PageSizeSelector_NavigatesOnChange()
    {
        var cut = Render<Pagination>(p => p
            .Add(x => x.CurrentPage, 1).Add(x => x.TotalPages, 6).Add(x => x.PageSize, 10)
            .Add(x => x.ShowPageSizeSelector, true).Add(x => x.BaseUrl, "/items"));

        var nav = Services.GetRequiredService<NavigationManager>();
        cut.Find("select#pageSize").Change("50");

        nav.Uri.Should().Contain("page=1").And.Contain("pageSize=50");
    }

    [Fact]
    public void LinkMode_ExistingPageQueryParam_IsStripped()
    {
        var cut = Render<Pagination>(p => p
            .Add(x => x.CurrentPage, 1).Add(x => x.TotalPages, 3).Add(x => x.BaseUrl, "/items?page=1&sort=name"));

        var link = cut.Find("a[aria-label='Next page']");
        link.GetAttribute("href").Should().Be("/items?sort=name&page=2");
    }

    // ---- Callback mode (BaseUrl null) ------------------------------------------------------------

    [Fact]
    public void CallbackMode_RendersButtons_NotAnchors()
    {
        var cut = Render<Pagination>(p => p.Add(x => x.CurrentPage, 2).Add(x => x.TotalPages, 5));

        cut.FindAll("a").Should().BeEmpty();
        cut.FindAll("button").Should().NotBeEmpty();
    }

    [Fact]
    public void CallbackMode_ClickingPageNumber_InvokesOnPageChanged()
    {
        var clicked = -1;
        var cut = Render<Pagination>(p => p
            .Add(x => x.CurrentPage, 3).Add(x => x.TotalPages, 5)
            .Add(x => x.OnPageChanged, EventCallback.Factory.Create<int>(this, page => clicked = page)));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "2").Click();

        clicked.Should().Be(2);
    }

    [Fact]
    public void CallbackMode_FirstPage_DisablesPreviousButton()
    {
        var cut = Render<Pagination>(p => p.Add(x => x.CurrentPage, 1).Add(x => x.TotalPages, 5));
        cut.Find("button[aria-label='Previous page']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void CallbackMode_LastPage_DisablesNextButton()
    {
        var cut = Render<Pagination>(p => p.Add(x => x.CurrentPage, 5).Add(x => x.TotalPages, 5));
        cut.Find("button[aria-label='Next page']").HasAttribute("disabled").Should().BeTrue();
    }

    [Fact]
    public void CallbackMode_PageSizeSelector_InvokesOnPageSizeChanged()
    {
        var newSize = -1;
        var cut = Render<Pagination>(p => p
            .Add(x => x.CurrentPage, 1).Add(x => x.TotalPages, 6).Add(x => x.ShowPageSizeSelector, true)
            .Add(x => x.OnPageSizeChanged, EventCallback.Factory.Create<int>(this, s => newSize = s)));

        cut.Find("select#pageSize").Change("50");

        newSize.Should().Be(50);
    }

    [Fact]
    public void CallbackMode_WithoutOnPageChanged_DoesNotThrow()
    {
        var cut = Render<Pagination>(p => p.Add(x => x.CurrentPage, 2).Add(x => x.TotalPages, 5));
        var act = () => cut.FindAll("button").First(b => b.TextContent.Trim() == "1").Click();
        act.Should().NotThrow();
    }

    // ---- Shared -----------------------------------------------------------------------------------

    [Fact]
    public void Class_And_AdditionalAttributes_ArePassedThrough_OnEveryStyle()
    {
        foreach (var style in new[] { PaginationStyle.Simple, PaginationStyle.Compact, PaginationStyle.Bordered, PaginationStyle.Full })
        {
            var cut = Render<Pagination>(p => p
                .Add(x => x.CurrentPage, 2).Add(x => x.TotalPages, 5).Add(x => x.Style, style)
                .Add(x => x.Class, "extra").AddUnmatched("data-testid", "pagination"));

            var root = cut.Find("[data-testid='pagination']");
            root.ClassList.Should().Contain("extra");
        }
    }
}
