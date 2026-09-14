using System.Security.Claims;
using Bunit;
using DiscordBot.Bot.Blazor.Portal;
using DiscordBot.Bot.Services.Portal;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Portal;

/// <summary>
/// Component tests for <see cref="PortalPageBase"/>, exercised through the minimal
/// <see cref="TestPortalPage"/> host - mirrors <c>GuildPageBaseTests</c>'s coverage for the
/// guild-side equivalent: one provider call on initial render, no re-resolution when
/// <c>GuildId</c> is unchanged, re-resolution when it changes, and the persist/restore round trip
/// across a simulated prerender-to-circuit boundary.
/// </summary>
public class PortalPageBaseTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;
    private const ulong OtherGuildId = 987654321098765432UL;

    private static PortalContext Context(ulong guildId, string name) => new(
        Guild: new GuildDto { Id = guildId, Name = name },
        GuildIdString: guildId.ToString(),
        GuildName: name,
        IconUrl: null,
        IsBotOnline: true);

    private Mock<IPortalContextProvider> RegisterMockProvider()
    {
        var mock = new Mock<IPortalContextProvider>();
        Services.AddScoped(_ => mock.Object);
        return mock;
    }

    [Fact]
    public void OnInitialized_ResolvesOnce_AndRendersChild_WhenAuthorized()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("member");
        var mockProvider = RegisterMockProvider();
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.Authorized(Context(GuildId, "Test Guild"), string.Empty));

        var cut = Render<TestPortalPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Find("[data-testid='outcome']").TextContent.Should().Be("Authorized");
        cut.Find("[data-testid='guild-name']").TextContent.Should().Be("Test Guild");
        cut.Find("[data-testid='ready-count']").TextContent.Should().Be("1");
        mockProvider.Verify(
            p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void OnInitialized_RendersGuildNotFoundOutcome_WithoutGuildName()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetNotAuthorized();
        var mockProvider = RegisterMockProvider();
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.GuildNotFound());

        var cut = Render<TestPortalPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Find("[data-testid='outcome']").TextContent.Should().Be("GuildNotFound");
        cut.FindAll("[data-testid='guild-name']").Should().BeEmpty();
    }

    [Fact]
    public void Render_WithSameGuildId_DoesNotReResolve()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("member");
        var mockProvider = RegisterMockProvider();
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.Authorized(Context(GuildId, "Test Guild"), string.Empty));

        var cut = Render<TestPortalPage>(p => p.Add(x => x.GuildId, (long)GuildId));
        cut.Render(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Find("[data-testid='ready-count']").TextContent.Should().Be("1");
        mockProvider.Verify(
            p => p.GetAsync(It.IsAny<ulong>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Render_WithDifferentGuildId_ReResolves()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("member");
        var mockProvider = RegisterMockProvider();
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.Authorized(Context(GuildId, "First Guild"), string.Empty));
        mockProvider
            .Setup(p => p.GetAsync(OtherGuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.Authorized(Context(OtherGuildId, "Second Guild"), string.Empty));

        var cut = Render<TestPortalPage>(p => p.Add(x => x.GuildId, (long)GuildId));
        cut.Find("[data-testid='guild-name']").TextContent.Should().Be("First Guild");

        cut.Render(p => p.Add(x => x.GuildId, (long)OtherGuildId));

        cut.Find("[data-testid='guild-name']").TextContent.Should().Be("Second Guild");
        cut.Find("[data-testid='ready-count']").TextContent.Should().Be("2");
    }

    [Fact]
    public void PersistedResult_IsReused_AcrossASimulatedPrerenderToCircuitBoundary()
    {
        var persistentState = AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("member");
        var mockProvider = RegisterMockProvider();
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.Authorized(Context(GuildId, "Test Guild"), string.Empty));

        var prerendered = Render<TestPortalPage>(p => p.Add(x => x.GuildId, (long)GuildId));
        prerendered.Find("[data-testid='guild-name']").TextContent.Should().Be("Test Guild");

        persistentState.TriggerOnPersisting();

        var reconnected = Render<TestPortalPage>(p => p.Add(x => x.GuildId, (long)GuildId));
        reconnected.Find("[data-testid='guild-name']").TextContent.Should().Be("Test Guild");

        mockProvider.Verify(
            p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "the reconnected instance should restore from persisted state, not call the provider again");
    }
}
