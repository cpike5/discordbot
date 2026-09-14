using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Layout;
using DiscordBot.Bot.Configuration;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Layout;

/// <summary>
/// Component tests for <see cref="GuildLayout"/>. bUnit does not apply a component's
/// <c>[Layout]</c> attribute (that is interpreted by the Router/AuthorizeRouteView, not by direct
/// instantiation) - <c>Render&lt;GuildLayout&gt;</c> renders the layout on its own, the same way
/// <c>MainLayoutTests</c> renders <c>MainLayout</c> without its own ancestor, so no MainLayout
/// dependencies need registering here.
/// </summary>
public class GuildLayoutTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private static GuildContext OkContext(bool includeTabs = true) => new(
        Guild: new GuildDto { Id = GuildId, Name = "Test Guild", IconUrl = "https://cdn.example/icon.png" },
        GuildId: GuildId,
        GuildIdString: GuildId.ToString(),
        IsAppAdmin: false,
        IsGuildAdmin: true,
        CanEdit: true,
        AudioEnabled: true,
        RatWatchEnabled: false,
        Tabs: includeTabs ? DiscordBot.Bot.Configuration.GuildNavigationConfig.GetTabs() : []);

    private Mock<IGuildContextProvider> RegisterMockProvider()
    {
        var mock = new Mock<IGuildContextProvider>();
        Services.AddScoped(_ => mock.Object);
        return mock;
    }

    private void NavigateTo(string path)
        => ((BunitNavigationManager)Services.GetRequiredService<NavigationManager>()).NavigateTo(path);

    [Fact]
    public void NoGuildIdInRoute_RendersBodyOnly_WithNoChrome()
    {
        var mockProvider = RegisterMockProvider();
        NavigateTo("/");

        var cut = Render<GuildLayout>(p => p
            .Add(x => x.Body, "<p data-testid=\"layout-body\">Page content</p>"));

        cut.Find("[data-testid='layout-body']").TextContent.Should().Be("Page content");
        cut.FindAll("nav[aria-label='Breadcrumb']").Should().BeEmpty();
        mockProvider.Verify(
            p => p.GetAsync(It.IsAny<ulong>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Ok_RendersBreadcrumbHeaderAndTabNav_WithActiveTabMarked()
    {
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Guilds/{GuildId}/Members");
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(OkContext()));

        var cut = Render<GuildLayout>(p => p
            .Add(x => x.Body, "<p data-testid=\"layout-body\">Members page</p>"));

        cut.Find("nav[aria-label='Breadcrumb']").Should().NotBeNull();
        cut.Markup.Should().Contain("Test Guild");
        cut.Find("[data-testid='layout-body']").TextContent.Should().Be("Members page");

        // Header title is the active tab's label, per GuildLayout's own documented "no per-page
        // title reaches this layout" decision.
        cut.Find("h1").TextContent.Should().Be("Members");

        var activeTab = cut.Find("[data-tab-id='members'][aria-selected='true']");
        activeTab.Should().NotBeNull();
        // NavLink's own built-in active-link detection independently agrees this href is current.
        activeTab.GetAttribute("aria-current").Should().Be("page");
    }

    [Fact]
    public void Ok_OverviewTab_UsesPlainBreadcrumb_WithGuildNameAsHeaderTitle()
    {
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Guilds/Details/{GuildId}");
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(OkContext()));

        var cut = Render<GuildLayout>(p => p
            .Add(x => x.Body, "<p data-testid=\"layout-body\">Overview page</p>"));

        // BuildBasicBreadcrumb shape: guild name is itself the current (last) breadcrumb item -
        // no trailing "Overview" segment.
        var breadcrumbItems = cut.FindAll("nav[aria-label='Breadcrumb'] li");
        breadcrumbItems.Should().HaveCount(3);
        cut.Find("h1").TextContent.Should().Be("Test Guild");
    }

    [Fact]
    public void NotFound_RendersBodyOnly_OmittingHeaderAndNav()
    {
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Guilds/{GuildId}/Members");
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.NotFound());

        var cut = Render<GuildLayout>(p => p
            .Add(x => x.Body, "<p data-testid=\"layout-body\">Gate shows 404 here</p>"));

        cut.Find("[data-testid='layout-body']").TextContent.Should().Be("Gate shows 404 here");
        cut.FindAll("nav[aria-label='Breadcrumb']").Should().BeEmpty();
        cut.FindAll("[role='tablist']").Should().BeEmpty();
    }

    [Fact]
    public void Forbidden_RendersBodyOnly_OmittingHeaderAndNav()
    {
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Guilds/{GuildId}/Members");
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Forbidden());

        var cut = Render<GuildLayout>(p => p
            .Add(x => x.Body, "<p data-testid=\"layout-body\">Gate shows 403 here</p>"));

        cut.Find("[data-testid='layout-body']").TextContent.Should().Be("Gate shows 403 here");
        cut.FindAll("nav[aria-label='Breadcrumb']").Should().BeEmpty();
        cut.FindAll("[role='tablist']").Should().BeEmpty();
    }

    [Fact]
    public void MobileNavSelect_ListsEveryTab_WithActiveTabSelected()
    {
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Guilds/{GuildId}/Members");
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(OkContext()));

        var cut = Render<GuildLayout>(p => p
            .Add(x => x.Body, "<p>Body</p>"));

        var select = cut.Find("#guildNavMobileSelect");
        select.GetAttribute("data-shell-action").Should().Be("navigate-select");

        var options = select.QuerySelectorAll("option");
        options.Should().HaveCount(GuildNavigationConfig.GetTabs().Count);

        var selectedOption = select.QuerySelector("option[selected]");
        selectedOption.Should().NotBeNull();
        selectedOption!.TextContent.Should().Be("Members");
    }
}
