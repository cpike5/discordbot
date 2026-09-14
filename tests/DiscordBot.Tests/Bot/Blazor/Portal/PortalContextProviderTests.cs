using System.Security.Claims;
using DiscordBot.Bot.Blazor.Portal;
using DiscordBot.Bot.Services.Portal;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Moq;

namespace DiscordBot.Tests.Bot.Blazor.Portal;

/// <summary>
/// Unit tests for <see cref="PortalContextProvider"/>: a thin memoising wrapper over
/// <see cref="IPortalAccessService"/>, mirroring <c>GuildContextProviderTests</c>'s memoisation
/// coverage for the guild-side equivalent. See "Portal three-state gate" in
/// <c>docs/architecture/patterns.md</c>.
/// </summary>
public class PortalContextProviderTests
{
    private const ulong GuildId = 123456789012345678UL;
    private const ulong OtherGuildId = 987654321098765432UL;

    private readonly Mock<IPortalAccessService> _mockAccessService;
    private readonly PortalContextProvider _provider;

    public PortalContextProviderTests()
    {
        _mockAccessService = new Mock<IPortalAccessService>();
        _provider = new PortalContextProvider(_mockAccessService.Object);
    }

    private static PortalContext Context(ulong guildId, string name) => new(
        Guild: new GuildDto { Id = guildId, Name = name },
        GuildIdString: guildId.ToString(),
        GuildName: name,
        IconUrl: null,
        IsBotOnline: true);

    private static ClaimsPrincipal User() => new(new ClaimsIdentity());

    [Fact]
    public async Task GetAsync_DelegatesToPortalAccessService()
    {
        var expected = PortalAccessResult.Authorized(Context(GuildId, "Test Guild"), string.Empty);
        _mockAccessService
            .Setup(s => s.ResolveAsync(GuildId, It.IsAny<ClaimsPrincipal>(), "/Portal/Soundboard/x", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _provider.GetAsync(GuildId, User(), "/Portal/Soundboard/x");

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetAsync_Memoises_OneResolveCall_ForTwoCallsWithSameGuildId()
    {
        _mockAccessService
            .Setup(s => s.ResolveAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.Authorized(Context(GuildId, "Test Guild"), string.Empty));

        var user = User();
        var first = await _provider.GetAsync(GuildId, user, "/Portal/Soundboard/x");
        var second = await _provider.GetAsync(GuildId, user, "/Portal/Soundboard/x");

        first.Should().BeSameAs(second, "the second call should be served from the memoisation cache");
        _mockAccessService.Verify(
            s => s.ResolveAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAsync_DoesNotMemoiseAcrossDifferentGuildIds()
    {
        _mockAccessService
            .Setup(s => s.ResolveAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.Authorized(Context(GuildId, "Test Guild"), string.Empty));
        _mockAccessService
            .Setup(s => s.ResolveAsync(OtherGuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PortalAccessResult.Authorized(Context(OtherGuildId, "Other Guild"), string.Empty));

        var user = User();
        await _provider.GetAsync(GuildId, user, "/Portal/Soundboard/x");
        await _provider.GetAsync(OtherGuildId, user, "/Portal/Soundboard/y");

        _mockAccessService.Verify(
            s => s.ResolveAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockAccessService.Verify(
            s => s.ResolveAsync(OtherGuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
