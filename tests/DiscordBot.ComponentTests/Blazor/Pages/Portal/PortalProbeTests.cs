using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Pages.Portal;
using DiscordBot.Bot.Blazor.Portal;
using DiscordBot.Bot.Services.Portal;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Portal;

/// <summary>
/// Component tests for the temporary Phase 3 probe page <see cref="PortalProbe"/>, proving
/// <c>PortalPageBase</c> renders correctly for a real routable, anonymous-reachable Portal page.
/// </summary>
public class PortalProbeTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private static PortalContext Context() => new(
        Guild: new GuildDto { Id = GuildId, Name = "Test Guild" },
        GuildIdString: GuildId.ToString(),
        GuildName: "Test Guild",
        IconUrl: null,
        IsBotOnline: true);

    private Mock<IPortalContextProvider> RegisterMockProvider()
    {
        var mock = new Mock<IPortalContextProvider>();
        Services.AddScoped(_ => mock.Object);
        return mock;
    }

    private void NavigateTo(string path)
        => ((BunitNavigationManager)Services.GetRequiredService<NavigationManager>()).NavigateTo(path);

    [Fact]
    public void Authorized_RendersResolvedPortalContext()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("member");
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Portal/{GuildId}/blazor-probe");
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.Authorized(Context(), string.Empty));

        var cut = Render<PortalProbe>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Find("[data-testid='probe-portal-outcome']").TextContent.Should().Be("Authorized");
        cut.Find("[data-testid='probe-portal-guild-name']").TextContent.Should().Be("Test Guild");
        cut.Find("[data-testid='probe-portal-guild-id']").TextContent.Should().Be(GuildId.ToString());
        cut.Find("[data-testid='probe-portal-bot-online']").TextContent.Should().Be("True");
    }

    [Fact]
    public void GuildNotFound_RendersOutcome_WithNoGuildFields()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetNotAuthorized();
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Portal/{GuildId}/blazor-probe");
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.GuildNotFound());

        var cut = Render<PortalProbe>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Find("[data-testid='probe-portal-outcome']").TextContent.Should().Be("GuildNotFound");
        cut.FindAll("[data-testid='probe-portal-guild-name']").Should().BeEmpty();
    }

    [Fact]
    public void ShowLanding_RendersOutcome_WithLoginUrl()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetNotAuthorized();
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Portal/{GuildId}/blazor-probe");
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.ShowLanding(Context(), "/Account/Login?returnUrl=x"));

        var cut = Render<PortalProbe>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Find("[data-testid='probe-portal-outcome']").TextContent.Should().Be("ShowLanding");
        cut.Find("[data-testid='probe-portal-login-url']").TextContent.Should().Be("/Account/Login?returnUrl=x");
    }
}
