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

    /// <summary>
    /// Regression coverage for the Phase 3 review finding: a faulted resolution used to stay the
    /// cached entry for the rest of the scope, so a transient failure permanently poisoned every
    /// later call for that guild id. <see cref="PortalContextProvider"/> now evicts the cache
    /// entry once the underlying task faults, so a later call re-resolves instead of rethrowing
    /// the same exception forever. See <c>GuildContextProviderTests</c>'s analogous test for why
    /// this polls rather than asserting immediately after the first call's exception.
    /// </summary>
    [Fact]
    public async Task GetAsync_EvictsCachedEntry_WhenResolutionFaults_SoALaterCallRetries()
    {
        var callCount = 0;
        _mockAccessService
            .Setup(s => s.ResolveAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                Interlocked.Increment(ref callCount);
                return callCount == 1
                    ? Task.FromException<PortalAccessResult>(new InvalidOperationException("transient failure"))
                    : Task.FromResult(PortalAccessResult.Authorized(Context(GuildId, "Test Guild"), string.Empty));
            });

        var user = User();

        var firstCall = async () => await _provider.GetAsync(GuildId, user, "/Portal/Soundboard/x");
        await firstCall.Should().ThrowAsync<InvalidOperationException>();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        PortalAccessResult? result = null;
        while (result is null)
        {
            try
            {
                result = await _provider.GetAsync(GuildId, user, "/Portal/Soundboard/x");
            }
            catch (InvalidOperationException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(10).ConfigureAwait(false);
            }
        }

        result.Outcome.Should().Be(PortalAccessOutcome.Authorized);
        callCount.Should().Be(2, "the faulted first resolution must not be served again - the retry should call ResolveAsync a second time");
    }
}
