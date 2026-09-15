using System.Security.Claims;
using Discord.WebSocket;
using DiscordBot.Bot.Pages.Portal;
using DiscordBot.Bot.Services.Portal;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Portal;

/// <summary>
/// Unit tests for <see cref="PortalPageModelBase.CheckPortalAuthorizationAsync"/>. Regression
/// coverage for the Phase 3 review finding: after the refactor to <see cref="IPortalAccessService"/>,
/// an <see cref="PortalAccessOutcome.Authorized"/> result whose guild has fallen out of the
/// Discord gateway cache between the service's own existence check and this class's re-lookup
/// (<c>_discordClient.GetGuild(guildId)</c>) used to build a null <c>PortalAuthContext</c> that
/// Soundboard/TTS/VOX's <c>context!.SocketGuild</c> then NREs on. <c>CheckPortalAuthorizationAsync</c>
/// now short-circuits to <see cref="PortalPageModelBase.PortalAuthResult.GuildNotFound"/> whenever
/// the re-lookup comes back null, for every outcome, matching the original inline behavior before
/// the <see cref="IPortalAccessService"/> extraction.
/// </summary>
public class PortalPageModelBaseTests
{
    private const ulong GuildId = 123456789012345678UL;

    /// <summary>
    /// Both <c>PortalAuthResult</c> and <c>PortalAuthContext</c> are <c>protected</c> nested
    /// types on <see cref="PortalPageModelBase"/>, so they cannot appear in a public member
    /// signature reachable from outside the inheritance chain - this record is the externally
    /// visible projection <see cref="TestablePortalPageModel.CheckAsync"/> returns instead.
    /// </summary>
    private sealed record PortalCheckOutcome(bool IsGuildNotFound, bool HasContext, IActionResult? ActionResult);

    /// <summary>
    /// Exposes the protected members under test. <see cref="DiscordSocketClient"/> is sealed-ish
    /// enough that Moq can proxy it (see <c>PortalAccessServiceTests</c>), but a plain,
    /// never-connected instance is simpler here and gives the exact behavior this test needs for
    /// free: <c>GetGuild</c> always returns <c>null</c> because nothing has ever populated its
    /// gateway cache - i.e. exactly "the gateway cache lost/never had the guild".
    /// </summary>
    private sealed class TestablePortalPageModel : PortalPageModelBase
    {
        public TestablePortalPageModel(
            IGuildService guildService, DiscordSocketClient discordClient, UserManager<ApplicationUser> userManager, ILogger logger)
            : base(guildService, discordClient, userManager, logger)
        {
        }

        public async Task<PortalCheckOutcome> CheckAsync(ulong guildId, string portalName, CancellationToken ct = default)
        {
            var (result, context) = await CheckPortalAuthorizationAsync(guildId, portalName, ct);
            return new PortalCheckOutcome(
                IsGuildNotFound: result == PortalAuthResult.GuildNotFound,
                HasContext: context is not null,
                ActionResult: GetAuthResultAction(result));
        }
    }

    private static TestablePortalPageModel CreateModel(IPortalAccessService accessService, DiscordSocketClient discordClient)
    {
        var services = new ServiceCollection();
        services.AddSingleton(accessService);
        var provider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider,
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };

        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object,
            null!,
            new Mock<IPasswordHasher<ApplicationUser>>().Object,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new Mock<ILookupNormalizer>().Object,
            new Mock<IdentityErrorDescriber>().Object,
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        var model = new TestablePortalPageModel(
            Mock.Of<IGuildService>(),
            discordClient,
            mockUserManager.Object,
            Mock.Of<ILogger>());
        model.PageContext = new PageContext { HttpContext = httpContext };
        return model;
    }

    private static PortalContext Context() => new(
        Guild: new GuildDto { Id = GuildId, Name = "Test Guild" },
        GuildIdString: GuildId.ToString(),
        GuildName: "Test Guild",
        IconUrl: null,
        IsBotOnline: true);

    [Theory]
    [InlineData(PortalAccessOutcome.Authorized)]
    [InlineData(PortalAccessOutcome.ShowLanding)]
    [InlineData(PortalAccessOutcome.NotGuildMember)]
    public async Task CheckPortalAuthorizationAsync_ReturnsGuildNotFound_WhenSocketGuildMissingFromGatewayCache(
        PortalAccessOutcome accessOutcome)
    {
        var mockAccessService = new Mock<IPortalAccessService>();
        var result = accessOutcome switch
        {
            PortalAccessOutcome.Authorized => PortalAccessResult.Authorized(Context(), "/Account/Login"),
            PortalAccessOutcome.ShowLanding => PortalAccessResult.ShowLanding(Context(), "/Account/Login"),
            _ => PortalAccessResult.NotGuildMember(Context(), "/Account/Login")
        };
        mockAccessService
            .Setup(s => s.ResolveAsync(GuildId, It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Never connected/logged in - GetGuild(GuildId) has nothing cached and returns null,
        // simulating the guild falling out of the gateway cache after IPortalAccessService's own
        // existence check ran.
        using var discordClient = new DiscordSocketClient(new DiscordSocketConfig());
        var model = CreateModel(mockAccessService.Object, discordClient);

        var outcome = await model.CheckAsync(GuildId, "Soundboard");

        outcome.IsGuildNotFound.Should().BeTrue(
            "a missing SocketGuild must fall back to GuildNotFound regardless of the resolved outcome, " +
            "so Soundboard/TTS/VOX never see an Authorized/ShowLanding/NotGuildMember result with a null context");
        outcome.HasContext.Should().BeFalse();
        outcome.ActionResult.Should().BeOfType<NotFoundResult>();
    }
}
