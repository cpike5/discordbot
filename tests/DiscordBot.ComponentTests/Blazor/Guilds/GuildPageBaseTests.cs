using System.Security.Claims;
using Bunit;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Guilds;

/// <summary>
/// Component tests for <see cref="GuildPageBase"/>, exercised through the minimal
/// <see cref="TestGuildPage"/> host (no real routable guild page exists yet - <c>GuildLayout</c>
/// and the first ported guild page are built on top of this type in a later PR). Covers: one
/// provider call on initial render, the gate states via <see cref="GuildContextResult.Status"/>,
/// no re-resolution when <c>GuildId</c> is unchanged, re-resolution when it changes, and the
/// persist/restore round trip across a simulated prerender-to-circuit boundary.
/// </summary>
/// <remarks>
/// <see cref="BlazorComponentTestContext"/> does not register <c>PersistentComponentState</c> by
/// default (bUnit only wires it up via the opt-in <c>AddBunitPersistentComponentState()</c>), so
/// every test here calls it explicitly before the first render.
/// </remarks>
public class GuildPageBaseTests : BlazorComponentTestContext
{
    private const ulong GuildId = 123456789012345678UL;
    private const ulong OtherGuildId = 987654321098765432UL;

    private static GuildContext Context(ulong guildId, string name) => new(
        Guild: new GuildDto { Id = guildId, Name = name },
        GuildId: guildId,
        GuildIdString: guildId.ToString(),
        IsAppAdmin: false,
        IsGuildAdmin: false,
        CanEdit: false,
        AudioEnabled: false,
        RatWatchEnabled: false,
        Tabs: Array.Empty<DiscordBot.Bot.ViewModels.Components.GuildNavItem>());

    private Mock<IGuildContextProvider> RegisterMockProvider()
    {
        var mock = new Mock<IGuildContextProvider>();
        Services.AddScoped(_ => mock.Object);
        return mock;
    }

    [Fact]
    public void OnInitialized_ResolvesOnce_AndRendersChild_WhenAuthorized()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("admin").SetRoles("Admin");
        var mockProvider = RegisterMockProvider();
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(Context(GuildId, "Test Guild")));

        var cut = Render<TestGuildPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Find("[data-testid='status']").TextContent.Should().Be("Ok");
        cut.Find("[data-testid='guild-name']").TextContent.Should().Be("Test Guild");
        cut.Find("[data-testid='ready-count']").TextContent.Should().Be("1");
        mockProvider.Verify(
            p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void OnInitialized_RendersNotFoundStatus_WithoutGuildName()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("admin").SetRoles("Admin");
        var mockProvider = RegisterMockProvider();
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.NotFound());

        var cut = Render<TestGuildPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Find("[data-testid='status']").TextContent.Should().Be("NotFound");
        cut.FindAll("[data-testid='guild-name']").Should().BeEmpty();
    }

    [Fact]
    public void OnInitialized_RendersForbiddenStatus_WithoutGuildName()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("member").SetRoles("Member");
        var mockProvider = RegisterMockProvider();
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Forbidden());

        var cut = Render<TestGuildPage>(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Find("[data-testid='status']").TextContent.Should().Be("Forbidden");
        cut.FindAll("[data-testid='guild-name']").Should().BeEmpty();
    }

    [Fact]
    public void Render_WithSameGuildId_DoesNotReResolve()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("admin").SetRoles("Admin");
        var mockProvider = RegisterMockProvider();
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(Context(GuildId, "Test Guild")));

        var cut = Render<TestGuildPage>(p => p.Add(x => x.GuildId, (long)GuildId));
        cut.Render(p => p.Add(x => x.GuildId, (long)GuildId));

        cut.Find("[data-testid='ready-count']").TextContent.Should().Be("1");
        mockProvider.Verify(
            p => p.GetAsync(It.IsAny<ulong>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Render_WithDifferentGuildId_ReResolves()
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("admin").SetRoles("Admin");
        var mockProvider = RegisterMockProvider();
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(Context(GuildId, "First Guild")));
        mockProvider
            .Setup(p => p.GetAsync(OtherGuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(Context(OtherGuildId, "Second Guild")));

        var cut = Render<TestGuildPage>(p => p.Add(x => x.GuildId, (long)GuildId));
        cut.Find("[data-testid='guild-name']").TextContent.Should().Be("First Guild");

        cut.Render(p => p.Add(x => x.GuildId, (long)OtherGuildId));

        cut.Find("[data-testid='guild-name']").TextContent.Should().Be("Second Guild");
        cut.Find("[data-testid='ready-count']").TextContent.Should().Be("2");
        mockProvider.Verify(
            p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mockProvider.Verify(
            p => p.GetAsync(OtherGuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void PersistedResult_IsReused_AcrossASimulatedPrerenderToCircuitBoundary()
    {
        var persistentState = AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("admin").SetRoles("Admin");
        var mockProvider = RegisterMockProvider();
        mockProvider
            .Setup(p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GuildContextResult.Ok(Context(GuildId, "Test Guild")));

        // First pass ("prerender"): resolves via the provider and registers its persisting
        // callback.
        var prerendered = Render<TestGuildPage>(p => p.Add(x => x.GuildId, (long)GuildId));
        prerendered.Find("[data-testid='guild-name']").TextContent.Should().Be("Test Guild");

        // Simulates the end of the prerender phase, where the framework invokes every
        // RegisterOnPersisting callback to serialize state into the page.
        persistentState.TriggerOnPersisting();

        // Second pass ("circuit reconnect"): a fresh component instance, same DI scope - should
        // find the persisted GuildContextResult via TryTakeFromJson instead of calling the
        // provider a second time.
        var reconnected = Render<TestGuildPage>(p => p.Add(x => x.GuildId, (long)GuildId));
        reconnected.Find("[data-testid='guild-name']").TextContent.Should().Be("Test Guild");

        mockProvider.Verify(
            p => p.GetAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "the reconnected instance should restore from persisted state, not call the provider again");
    }

    /// <summary>
    /// Regression coverage for the Phase 3 review finding: <c>{guildId:long}</c> accepts zero and
    /// negative values, which <c>GuildRoutes.TryGetGuildId</c>'s <c>\d+</c> regex never matches -
    /// <c>GuildLayout</c> would render no chrome for such a URL while this page, unguarded,
    /// unchecked-cast the negative/zero <c>long</c> to a huge, meaningless <c>ulong</c> and asked
    /// the provider to look it up anyway. <c>GuildPageBase</c> now short-circuits to NotFound
    /// without ever calling the provider.
    /// </summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void OnInitialized_RendersNotFound_WithoutCallingProvider_WhenGuildIdIsNonPositive(long guildId)
    {
        AddBunitPersistentComponentState();
        AddAuthorization().SetAuthorized("admin").SetRoles("Admin");
        var mockProvider = RegisterMockProvider();

        var cut = Render<TestGuildPage>(p => p.Add(x => x.GuildId, guildId));

        cut.Find("[data-testid='status']").TextContent.Should().Be("NotFound");
        cut.FindAll("[data-testid='guild-name']").Should().BeEmpty();
        mockProvider.Verify(
            p => p.GetAsync(It.IsAny<ulong>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
