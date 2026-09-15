using System.Security.Claims;
using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Services.Portal;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services.Portal;

/// <summary>
/// Unit tests for <see cref="PortalAccessService"/>. Behaviour must match the original inline
/// logic in <c>PortalPageModelBase.CheckPortalAuthorizationAsync</c> exactly (same ordering, same
/// bypasses) - see that class's XML doc. <see cref="DiscordGuildExists"/> and
/// <see cref="IsGuildMemberAsync"/> are exercised through the
/// <see cref="TestablePortalAccessService"/> seam rather than a real <c>SocketGuild</c>, which is
/// sealed and cannot be mocked (same caveat as <c>GuildAccessHandlerTests</c>).
/// </summary>
public class PortalAccessServiceTests
{
    private const ulong GuildId = 123456789012345678UL;
    private const ulong DiscordUserId = 555000111UL;
    private const string ReturnPath = "/Portal/Soundboard/123456789012345678";

    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<ILogger<PortalAccessService>> _mockLogger;

    public PortalAccessServiceTests()
    {
        _mockGuildService = new Mock<IGuildService>();
        _mockDiscordClient = new Mock<DiscordSocketClient>(MockBehavior.Default, new DiscordSocketConfig());
        _mockLogger = new Mock<ILogger<PortalAccessService>>();

        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object,
            null!,
            new Mock<IPasswordHasher<ApplicationUser>>().Object,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new Mock<ILookupNormalizer>().Object,
            new Mock<IdentityErrorDescriber>().Object,
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);
    }

    private static GuildDto Guild() => new()
    {
        Id = GuildId,
        Name = "Test Guild",
        IconUrl = "https://cdn.example/icon.png",
        IsActive = true
    };

    private TestablePortalAccessService CreateService(bool guildExists = true, bool isMember = true) =>
        new(
            _mockGuildService.Object,
            _mockDiscordClient.Object,
            _mockUserManager.Object,
            _mockLogger.Object,
            guildExists,
            isMember);

    private static ClaimsPrincipal AnonymousUser() => new(new ClaimsIdentity());

    private static ClaimsPrincipal AuthenticatedUser(params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "app-user-1") };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    [Fact]
    public async Task ResolveAsync_ReturnsGuildNotFound_WhenGuildMissingFromDatabase()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        var service = CreateService();
        var result = await service.ResolveAsync(GuildId, AnonymousUser(), ReturnPath);

        result.Outcome.Should().Be(PortalAccessOutcome.GuildNotFound);
        result.Context.Should().BeNull();
        result.LoginUrl.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_ReturnsGuildNotFound_WhenGuildMissingFromDiscordClient()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());

        var service = CreateService(guildExists: false);
        var result = await service.ResolveAsync(GuildId, AnonymousUser(), ReturnPath);

        result.Outcome.Should().Be(PortalAccessOutcome.GuildNotFound);
        result.Context.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ReturnsShowLanding_ForAnonymousUser()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());
        _mockDiscordClient.SetupGet(c => c.ConnectionState).Returns(ConnectionState.Connected);

        var service = CreateService();
        var result = await service.ResolveAsync(GuildId, AnonymousUser(), ReturnPath);

        result.Outcome.Should().Be(PortalAccessOutcome.ShowLanding);
        result.Context.Should().NotBeNull();
        result.Context!.GuildName.Should().Be("Test Guild");
        result.Context.IsBotOnline.Should().BeTrue();
        result.LoginUrl.Should().Be($"/Account/Login?returnUrl={Uri.EscapeDataString(ReturnPath)}");
    }

    [Fact]
    public async Task ResolveAsync_ReturnsShowLanding_WhenUserHasNoLinkedDiscordAccount()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());

        var principal = AuthenticatedUser();
        _mockUserManager
            .Setup(m => m.GetUserAsync(principal))
            .ReturnsAsync(new ApplicationUser { Id = "app-user-1", DiscordUserId = null });

        var service = CreateService();
        var result = await service.ResolveAsync(GuildId, principal, ReturnPath);

        result.Outcome.Should().Be(PortalAccessOutcome.ShowLanding);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsShowLanding_WhenApplicationUserNotFound()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());

        var principal = AuthenticatedUser();
        _mockUserManager
            .Setup(m => m.GetUserAsync(principal))
            .ReturnsAsync((ApplicationUser?)null);

        var service = CreateService();
        var result = await service.ResolveAsync(GuildId, principal, ReturnPath);

        result.Outcome.Should().Be(PortalAccessOutcome.ShowLanding);
    }

    [Theory]
    [InlineData(IdentitySeeder.Roles.SuperAdmin)]
    [InlineData(IdentitySeeder.Roles.Admin)]
    public async Task ResolveAsync_ReturnsAuthorized_ForAdminBypass_WithoutCheckingMembership(string role)
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());

        var principal = AuthenticatedUser(role);
        _mockUserManager
            .Setup(m => m.GetUserAsync(principal))
            .ReturnsAsync(new ApplicationUser { Id = "app-user-1", DiscordUserId = DiscordUserId });

        // isMember: false proves the bypass short-circuits before the membership seam matters.
        var service = CreateService(isMember: false);
        var result = await service.ResolveAsync(GuildId, principal, ReturnPath);

        result.Outcome.Should().Be(PortalAccessOutcome.Authorized);
        result.Context.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveAsync_ReturnsAuthorized_WhenUserIsGuildMember()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());

        var principal = AuthenticatedUser();
        _mockUserManager
            .Setup(m => m.GetUserAsync(principal))
            .ReturnsAsync(new ApplicationUser { Id = "app-user-1", DiscordUserId = DiscordUserId });

        var service = CreateService(isMember: true);
        var result = await service.ResolveAsync(GuildId, principal, ReturnPath);

        result.Outcome.Should().Be(PortalAccessOutcome.Authorized);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsAuthorized_WhenMembershipConfirmedOnlyViaRestFallback()
    {
        // The seam (IsGuildMemberAsync) stands in for "gateway cache miss, REST hit" - the
        // real cache-then-REST branch lives inside the un-overridden implementation and can't be
        // exercised here because SocketGuild is sealed (see the class doc).
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());

        var principal = AuthenticatedUser();
        _mockUserManager
            .Setup(m => m.GetUserAsync(principal))
            .ReturnsAsync(new ApplicationUser { Id = "app-user-1", DiscordUserId = DiscordUserId });

        var service = CreateService(isMember: true);
        var result = await service.ResolveAsync(GuildId, principal, ReturnPath);

        result.Outcome.Should().Be(PortalAccessOutcome.Authorized);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNotGuildMember_WhenUserIsNotAMember()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());

        var principal = AuthenticatedUser();
        _mockUserManager
            .Setup(m => m.GetUserAsync(principal))
            .ReturnsAsync(new ApplicationUser { Id = "app-user-1", DiscordUserId = DiscordUserId });

        var service = CreateService(isMember: false);
        var result = await service.ResolveAsync(GuildId, principal, ReturnPath);

        result.Outcome.Should().Be(PortalAccessOutcome.NotGuildMember);
        result.Context.Should().NotBeNull();
        result.LoginUrl.Should().NotBeEmpty();
    }

    /// <summary>
    /// Test double overriding the two Discord.Net seams with canned results, since
    /// <c>SocketGuild</c>/<c>SocketGuildUser</c> are sealed and cannot be mocked.
    /// </summary>
    private sealed class TestablePortalAccessService : PortalAccessService
    {
        private readonly bool _guildExists;
        private readonly bool _isMember;

        public TestablePortalAccessService(
            IGuildService guildService,
            DiscordSocketClient discordClient,
            UserManager<ApplicationUser> userManager,
            ILogger logger,
            bool guildExists,
            bool isMember)
            : base(guildService, discordClient, userManager, logger)
        {
            _guildExists = guildExists;
            _isMember = isMember;
        }

        protected override bool DiscordGuildExists(ulong guildId) => _guildExists;

        protected override Task<bool> IsGuildMemberAsync(ulong guildId, ulong discordUserId) =>
            Task.FromResult(_isMember);
    }
}
