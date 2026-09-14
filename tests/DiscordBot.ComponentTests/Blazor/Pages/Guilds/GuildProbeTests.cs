using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Bot.Blazor.Pages.Guilds;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Guilds;

/// <summary>
/// Component tests for the temporary Phase 3 probe page <see cref="GuildProbe"/>, proving
/// <c>GuildPageBase</c> and <c>GuildContextGate</c> render correctly together for a real routable
/// guild page (mirrors <c>BlazorProbeTests</c>'s role for the Phase 1 probe).
/// </summary>
public class GuildProbeTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;

    private static GuildContext Context() => new(
        Guild: new GuildDto { Id = GuildId, Name = "Test Guild" },
        GuildId: GuildId,
        GuildIdString: GuildId.ToString(),
        IsAppAdmin: true,
        IsGuildAdmin: false,
        CanEdit: true,
        AudioEnabled: true,
        RatWatchEnabled: true,
        Tabs: DiscordBot.Bot.Configuration.GuildNavigationConfig.GetTabs());

    private Mock<IGuildContextProvider> RegisterMockProvider()
    {
        var mock = new Mock<IGuildContextProvider>();
        Services.AddScoped(_ => mock.Object);
        return mock;
    }

    private void NavigateTo(string path)
        => ((BunitNavigationManager)Services.GetRequiredService<NavigationManager>()).NavigateTo(path);

    [Fact]
    public void Ok_RendersResolvedGuildContext()
    {
        AddBunitPersistentComponentState();
        AddAuthorizedAdmin();
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Guilds/{GuildId}/blazor-probe");
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(Context()));

        var cut = Render<GuildProbe>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Find("[data-testid='probe-guild-name']").TextContent.Should().Be("Test Guild");
        cut.Find("[data-testid='probe-guild-id']").TextContent.Should().Be(GuildId.ToString());
        cut.Find("[data-testid='probe-can-edit']").TextContent.Should().Be("True");
        cut.Find("[data-testid='probe-is-app-admin']").TextContent.Should().Be("True");
        cut.Find("[data-testid='probe-is-guild-admin']").TextContent.Should().Be("False");
        cut.Find("[data-testid='probe-audio-enabled']").TextContent.Should().Be("True");
        cut.Find("[data-testid='probe-ratwatch-enabled']").TextContent.Should().Be("True");
        cut.Find("[data-testid='probe-tab-count']").TextContent.Should().Be("11");

        // The probe route matches none of GuildNavigationConfig's registered tabs.
        cut.Find("[data-testid='probe-active-tab']").TextContent.Should().Be("(none)");
    }

    [Fact]
    public void NotFound_RendersGateNotFoundContent_NotProbeContent()
    {
        AddBunitPersistentComponentState();
        AddAuthorizedAdmin();
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Guilds/{GuildId}/blazor-probe");
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.NotFound());

        var cut = Render<GuildProbe>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Markup.Should().Contain("Server Not Found");
        cut.FindAll("[data-testid='probe-guild-context']").Should().BeEmpty();
    }

    [Fact]
    public void Forbidden_RendersGateForbiddenContent_NotProbeContent()
    {
        AddBunitPersistentComponentState();
        AddAuthorizedAdmin();
        var mockProvider = RegisterMockProvider();
        NavigateTo($"/Guilds/{GuildId}/blazor-probe");
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Forbidden());

        var cut = Render<GuildProbe>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Markup.Should().Contain("Access Denied");
        cut.FindAll("[data-testid='probe-guild-context']").Should().BeEmpty();
    }
}
