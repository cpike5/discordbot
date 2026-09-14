using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Layout;
using DiscordBot.Bot.Blazor.Portal;
using DiscordBot.Bot.Services.Portal;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Layout;

/// <summary>
/// Component tests for <see cref="PortalLayout"/>, covering all four
/// <see cref="PortalAccessOutcome"/> states plus the no-guild-id fallback. Same
/// direct-instantiation note as <see cref="GuildLayoutTests"/> - bUnit does not apply
/// <c>[Layout]</c>, so <c>Render&lt;PortalLayout&gt;</c> renders this layout on its own.
/// </summary>
public class PortalLayoutTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private static PortalContext Context(bool isOnline = true, string? iconUrl = "https://cdn.example/icon.png") => new(
        Guild: new GuildDto { Id = GuildId, Name = "Test Guild", IconUrl = iconUrl },
        GuildIdString: GuildId.ToString(),
        GuildName: "Test Guild",
        IconUrl: iconUrl,
        IsBotOnline: isOnline);

    private Mock<IPortalContextProvider> RegisterMockProvider()
    {
        var mock = new Mock<IPortalContextProvider>();
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

        var cut = Render<PortalLayout>(p => p
            .Add(x => x.Body, "<p data-testid=\"layout-body\">Page content</p>"));

        cut.Find("[data-testid='layout-body']").TextContent.Should().Be("Page content");
        mockProvider.Verify(
            p => p.GetAsync(It.IsAny<ulong>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void GuildNotFound_RendersEmptyState()
    {
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Portal/Soundboard/{GuildId}");
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.GuildNotFound());

        var cut = Render<PortalLayout>(p => p.Add(x => x.Body, "<p>Body</p>"));

        cut.Find("[data-testid='portal-guild-not-found']").Should().NotBeNull();
        cut.Markup.Should().Contain("Server Not Found");
    }

    [Fact]
    public void ShowLanding_RendersLandingContent_WithLoginLink()
    {
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Portal/Soundboard/{GuildId}");
        var context = Context();
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.ShowLanding(context, "/Account/Login?returnUrl=%2FPortal%2FSoundboard%2F123"));

        var cut = Render<PortalLayout>(p => p.Add(x => x.Body, "<p>Body</p>"));

        cut.Find("[data-testid='portal-landing']").Should().NotBeNull();
        cut.Markup.Should().Contain("Test Guild");
        var loginLink = cut.Find("[data-testid='portal-login-link']");
        loginLink.GetAttribute("href").Should().Be("/Account/Login?returnUrl=%2FPortal%2FSoundboard%2F123");
    }

    [Fact]
    public void NotGuildMember_RendersUnauthorizedContent()
    {
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Portal/Soundboard/{GuildId}");
        var context = Context();
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.NotGuildMember(context, "/Account/Login?returnUrl=%2FPortal%2FSoundboard%2F123"));

        var cut = Render<PortalLayout>(p => p.Add(x => x.Body, "<p>Body</p>"));

        cut.Find("[data-testid='portal-unauthorized']").Should().NotBeNull();
        cut.Markup.Should().Contain("Access Restricted");
        cut.Markup.Should().Contain("Test Guild");
    }

    [Fact]
    public void Authorized_RendersHeaderWithOnlineBadge_TabNavAndBody()
    {
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Portal/Soundboard/{GuildId}");
        var context = Context(isOnline: true);
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.Authorized(context, string.Empty));

        var cut = Render<PortalLayout>(p => p
            .Add(x => x.Body, "<p data-testid=\"layout-body\">Soundboard content</p>"));

        cut.Find("[data-testid='portal-header']").Should().NotBeNull();
        cut.Find("[data-testid='portal-guild-name']").TextContent.Should().Be("Test Guild Portal");

        var badge = cut.Find("[data-testid='portal-status-badge']");
        badge.ClassList.Should().Contain("online");
        badge.TextContent.Should().Contain("Online");

        cut.Find("[data-testid='layout-body']").TextContent.Should().Be("Soundboard content");

        var activeTab = cut.Find("[data-tab-id='soundboard'][aria-selected='true']");
        activeTab.Should().NotBeNull();
    }

    [Fact]
    public void Authorized_BotOffline_ShowsOfflineBadge()
    {
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Portal/TTS/{GuildId}");
        var context = Context(isOnline: false);
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.Authorized(context, string.Empty));

        var cut = Render<PortalLayout>(p => p.Add(x => x.Body, "<p>Body</p>"));

        var badge = cut.Find("[data-testid='portal-status-badge']");
        badge.ClassList.Should().Contain("offline");
        badge.TextContent.Should().Contain("Offline");

        var activeTab = cut.Find("[data-tab-id='tts'][aria-selected='true']");
        activeTab.Should().NotBeNull();
    }
}
