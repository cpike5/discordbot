using System.Security.Claims;
using Discord.WebSocket;
using DiscordBot.Bot.Authorization;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Bot.Authorization;

/// <summary>
/// Unit tests for <see cref="GuildAccessHandler"/>, the single (DI-registered) guild-access
/// authorization handler. Covers the cache-first / live-fallback-on-miss / refresh-on-live-hit
/// design: a cached <see cref="UserDiscordGuild"/> row or a sufficient explicit
/// <see cref="UserGuildAccess"/> grant is checked first, and only a genuine cache miss falls
/// back to a live Discord gateway lookup (via the <see cref="GuildAccessHandler.GetLiveGuildMembershipAsync"/>
/// seam, since Discord.Net's <c>SocketGuild</c>/<c>SocketGuildUser</c> are sealed and cannot be
/// mocked directly - see <c>RequireAdminAttributeTests</c> for the same caveat on the bot-command side).
/// </summary>
public class GuildAccessHandlerTests : IDisposable
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<IUserDiscordGuildService> _mockUserDiscordGuildService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<ILogger<GuildAccessHandler>> _mockLogger;
    private readonly BotDbContext _dbContext;
    private readonly SqliteConnection _connection;

    private const string UserId = "user123";
    private const ulong DiscordUserId = 555000111UL;
    private const ulong GuildId = 123456789012345678UL;

    private static readonly TimeSpan DefaultMembershipMaxAge = TimeSpan.FromHours(1);

    public GuildAccessHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new BotDbContext(options);
        _dbContext.Database.EnsureCreated();

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

        _mockDiscordClient = new Mock<DiscordSocketClient>();
        _mockUserDiscordGuildService = new Mock<IUserDiscordGuildService>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<GuildAccessHandler>>();

        _mockUserDiscordGuildService
            .Setup(s => s.GetUserGuildsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserDiscordGuild>().AsReadOnly());
    }

    private GuildAccessHandler CreateHandler(
        GuildAccessHandler.LiveGuildMembershipResult? liveResult = null,
        TimeSpan? membershipMaxAge = null)
        => new TestableGuildAccessHandler(
            _mockUserManager.Object,
            _mockDiscordClient.Object,
            _dbContext,
            _mockUserDiscordGuildService.Object,
            _mockHttpContextAccessor.Object,
            _mockLogger.Object,
            Options.Create(new GuildMembershipCacheOptions
            {
                MembershipMaxAge = membershipMaxAge ?? DefaultMembershipMaxAge
            }),
            liveResult);

    private static ClaimsPrincipal CreatePrincipal(string userId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private async Task<ApplicationUser> SeedLinkedUserAsync(string userId = UserId, ulong discordUserId = DiscordUserId)
    {
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@test.com",
            Email = $"{userId}@test.com",
            DiscordUserId = discordUserId
        };
        _dbContext.Set<ApplicationUser>().Add(user);
        await _dbContext.SaveChangesAsync();

        _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        return user;
    }

    private void SetHttpContextRoute(ulong? guildId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = guildId is null
            ? new RouteValueDictionary()
            : new RouteValueDictionary { ["guildId"] = guildId.Value.ToString() };
        _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);
    }

    private static AuthorizationHandlerContext CreateContext(
        ClaimsPrincipal user, GuildAccessRequirement requirement, object? resource = null)
        => new(new[] { requirement }, user, resource);

    // ---------------------------------------------------------------------------------------
    // SuperAdmin bypass
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task SuperAdmin_Bypasses_Succeeds()
    {
        var user = CreatePrincipal(UserId, IdentitySeeder.Roles.SuperAdmin);
        var handler = CreateHandler();
        var context = CreateContext(user, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("SuperAdmin should bypass all guild-specific checks");
    }

    // ---------------------------------------------------------------------------------------
    // Guild ID resolution order
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task ResourceUlong_TakesPrecedenceOverRoute_Succeeds()
    {
        var user = await SeedLinkedUserAsync();
        await SeedCachedMembershipAsync(GuildId);

        // Route points at a different guild the user has no access to; the resource ulong
        // must win so the check still succeeds against GuildId.
        SetHttpContextRoute(999999999999999999UL);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Viewer);
        var handler = CreateHandler();
        var context = CreateContext(principal, new GuildAccessRequirement(), resource: GuildId);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("the resource ulong should be used over the route value");
    }

    [Fact]
    public async Task RouteValue_ResolvesGuildId_Succeeds()
    {
        var user = await SeedLinkedUserAsync();
        await SeedCachedMembershipAsync(GuildId);
        SetHttpContextRoute(GuildId);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Viewer);
        var handler = CreateHandler();
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("the guildId route value should resolve the guild");
    }

    [Fact]
    public async Task QueryString_ResolvesGuildId_Succeeds()
    {
        await SeedLinkedUserAsync();
        await SeedCachedMembershipAsync(GuildId);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = new RouteValueDictionary();
        httpContext.Request.QueryString = new QueryString($"?guildId={GuildId}");
        _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Viewer);
        var handler = CreateHandler();
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("the guildId query string should resolve the guild");
    }

    [Fact]
    public async Task NoGuildIdAnywhere_Fails()
    {
        SetHttpContextRoute(null);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Viewer);
        var handler = CreateHandler();
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------
    // Linked-Discord-account requirement
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task UserWithoutLinkedDiscordAccount_Fails()
    {
        var user = new ApplicationUser { Id = UserId, UserName = "u@test.com", Email = "u@test.com" };
        _dbContext.Set<ApplicationUser>().Add(user);
        await _dbContext.SaveChangesAsync();
        _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

        SetHttpContextRoute(GuildId);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Viewer);
        var handler = CreateHandler();
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("a user without a linked Discord account cannot pass guild access");
    }

    // ---------------------------------------------------------------------------------------
    // Cache hit
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task CacheHit_ModeratorMembership_Succeeds()
    {
        await SeedLinkedUserAsync();
        await SeedCachedMembershipAsync(GuildId, administrator: false);
        SetHttpContextRoute(GuildId);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Moderator);
        var handler = CreateHandler();
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("Moderator only needs the cached membership to exist");
        _mockDiscordClient.Verify(c => c.GetGuild(It.IsAny<ulong>()), Times.Never,
            "a cache hit must not fall back to a live lookup");
    }

    [Fact]
    public async Task CacheHit_AdminWithCachedAdministratorPermission_Succeeds()
    {
        await SeedLinkedUserAsync();
        await SeedCachedMembershipAsync(GuildId, administrator: true);
        SetHttpContextRoute(GuildId);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Admin);
        var handler = CreateHandler();
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue(
            "the cached UserDiscordGuild.Permissions bitfield records the Administrator flag");
    }

    [Fact]
    public async Task CacheHit_AdminWithoutAdministratorPermission_Fails()
    {
        await SeedLinkedUserAsync();
        await SeedCachedMembershipAsync(GuildId, administrator: false);
        SetHttpContextRoute(GuildId);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Admin);
        var handler = CreateHandler();
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse(
            "Admin role requires the Administrator permission bit even with a cached membership row");
        _mockDiscordClient.Verify(c => c.GetGuild(It.IsAny<ulong>()), Times.Never,
            "cache-first design denies on an insufficient cache hit rather than falling back live");
    }

    [Fact]
    public async Task CacheHit_AdminWithoutPermissionButSufficientExplicitGrant_Succeeds()
    {
        await SeedLinkedUserAsync();
        var guild = new Guild { Id = GuildId, Name = "Test Guild", JoinedAt = DateTime.UtcNow };
        _dbContext.Set<Guild>().Add(guild);
        await SeedCachedMembershipAsync(GuildId, administrator: false);
        _dbContext.Set<UserGuildAccess>().Add(new UserGuildAccess
        {
            ApplicationUserId = UserId,
            GuildId = GuildId,
            AccessLevel = GuildAccessLevel.Admin,
            GrantedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        SetHttpContextRoute(GuildId);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Admin);
        var handler = CreateHandler();
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue(
            "a sufficient explicit UserGuildAccess grant covers an Admin lacking the cached Administrator bit");
    }

    // ---------------------------------------------------------------------------------------
    // Cache miss -> live fallback
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task CacheMiss_LiveHit_ModeratorSucceeds_AndRefreshesCache()
    {
        await SeedLinkedUserAsync();
        SetHttpContextRoute(GuildId);

        var live = new GuildAccessHandler.LiveGuildMembershipResult(
            IsAdministrator: false, GuildName: "Live Guild", GuildIconHash: "icon-hash",
            IsOwner: false, PermissionsRaw: 104324673);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Moderator);
        var handler = CreateHandler(live);
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("a live hit should grant Moderator access on a cache miss");
        _mockUserDiscordGuildService.Verify(
            s => s.UpsertGuildMembershipAsync(
                UserId,
                It.Is<DiscordGuildDto>(d => d.Id == GuildId && d.Name == "Live Guild"),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "a live hit must refresh the cached membership for next time");
    }

    [Fact]
    public async Task CacheMiss_LiveHit_AdminWithAdministratorPermission_Succeeds()
    {
        await SeedLinkedUserAsync();
        SetHttpContextRoute(GuildId);

        var live = new GuildAccessHandler.LiveGuildMembershipResult(
            IsAdministrator: true, GuildName: "Live Guild", GuildIconHash: null,
            IsOwner: false, PermissionsRaw: 8);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Admin);
        var handler = CreateHandler(live);
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task CacheMiss_LiveHit_AdminWithoutAdministratorPermission_Fails()
    {
        await SeedLinkedUserAsync();
        SetHttpContextRoute(GuildId);

        var live = new GuildAccessHandler.LiveGuildMembershipResult(
            IsAdministrator: false, GuildName: "Live Guild", GuildIconHash: null,
            IsOwner: false, PermissionsRaw: 0);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Admin);
        var handler = CreateHandler(live);
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse(
            "Admin role requires Discord Administrator permission, even on a live hit");

        // The membership is still refreshed - the user IS a member, just not an admin.
        _mockUserDiscordGuildService.Verify(
            s => s.UpsertGuildMembershipAsync(UserId, It.IsAny<DiscordGuildDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CacheMiss_LiveMiss_Fails()
    {
        await SeedLinkedUserAsync();
        SetHttpContextRoute(GuildId);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Viewer);
        var handler = CreateHandler(liveResult: null);
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("neither the cache nor the live gateway found a membership");
        _mockUserDiscordGuildService.Verify(
            s => s.UpsertGuildMembershipAsync(It.IsAny<string>(), It.IsAny<DiscordGuildDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "nothing to refresh on a live miss");
    }

    [Fact]
    public async Task CacheMiss_ExplicitGrantSufficient_SucceedsWithoutLiveLookup()
    {
        await SeedLinkedUserAsync();
        var guild = new Guild { Id = GuildId, Name = "Test Guild", JoinedAt = DateTime.UtcNow };
        _dbContext.Set<Guild>().Add(guild);
        _dbContext.Set<UserGuildAccess>().Add(new UserGuildAccess
        {
            ApplicationUserId = UserId,
            GuildId = GuildId,
            AccessLevel = GuildAccessLevel.Viewer,
            GrantedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        SetHttpContextRoute(GuildId);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Viewer);
        var handler = CreateHandler(); // no live result configured - must not be needed
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue(
            "an explicit grant meeting the requirement should succeed without needing a live lookup");
    }

    // ---------------------------------------------------------------------------------------
    // MembershipMaxAge: stale cached rows fall through to a live lookup
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task FreshCachedRow_DoesNotFallBackToLiveLookup()
    {
        await SeedLinkedUserAsync();
        await SeedCachedMembershipAsync(GuildId, administrator: false, lastUpdatedAt: DateTime.UtcNow.AddMinutes(-5));
        SetHttpContextRoute(GuildId);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Viewer);
        var handler = CreateHandler(membershipMaxAge: TimeSpan.FromHours(1));
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("a row well within MembershipMaxAge is a cache hit");
        _mockDiscordClient.Verify(c => c.GetGuild(It.IsAny<ulong>()), Times.Never,
            "a fresh cached row must not fall back to a live lookup");
    }

    [Fact]
    public async Task StaleCachedRow_LiveHit_Succeeds_AndRefreshesCache()
    {
        await SeedLinkedUserAsync();
        await SeedCachedMembershipAsync(GuildId, administrator: false, lastUpdatedAt: DateTime.UtcNow.AddHours(-2));
        SetHttpContextRoute(GuildId);

        var live = new GuildAccessHandler.LiveGuildMembershipResult(
            IsAdministrator: false, GuildName: "Refreshed Guild", GuildIconHash: null,
            IsOwner: false, PermissionsRaw: 0);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Viewer);
        var handler = CreateHandler(live, membershipMaxAge: TimeSpan.FromHours(1));
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue(
            "a stale cached row is treated as a cache miss, and the live lookup finds membership");
        _mockUserDiscordGuildService.Verify(
            s => s.UpsertGuildMembershipAsync(
                UserId,
                It.Is<DiscordGuildDto>(d => d.Id == GuildId && d.Name == "Refreshed Guild"),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "a live hit following a stale row must refresh the cache");
    }

    [Fact]
    public async Task StaleCachedRow_LiveMiss_Fails()
    {
        await SeedLinkedUserAsync();
        await SeedCachedMembershipAsync(GuildId, administrator: false, lastUpdatedAt: DateTime.UtcNow.AddHours(-2));
        SetHttpContextRoute(GuildId);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Viewer);
        var handler = CreateHandler(liveResult: null, membershipMaxAge: TimeSpan.FromHours(1));
        var context = CreateContext(principal, new GuildAccessRequirement());

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse(
            "the stale row is a cache miss, and the user is no longer a member on the live lookup " +
            "(e.g. kicked, banned, or left) - access must not be granted from the stale row");
        _mockUserDiscordGuildService.Verify(
            s => s.UpsertGuildMembershipAsync(It.IsAny<string>(), It.IsAny<DiscordGuildDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "nothing to refresh on a live miss; the stale row is left as-is");
    }

    // ---------------------------------------------------------------------------------------
    // MinimumLevel: plain membership must not bypass a stricter requirement
    // ---------------------------------------------------------------------------------------

    [Fact]
    public async Task CacheHit_PlainMembership_DeniedByHigherMinimumLevel()
    {
        await SeedLinkedUserAsync();
        await SeedCachedMembershipAsync(GuildId, administrator: false);
        SetHttpContextRoute(GuildId);

        // Moderator role membership only ever computes to the lowest (Viewer) effective level,
        // so a policy requiring more than Viewer must deny it even though the row is cached.
        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Moderator);
        var handler = CreateHandler();
        var context = CreateContext(principal, new GuildAccessRequirement(GuildAccessLevel.Moderator));

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse(
            "plain cached membership only satisfies the lowest (Viewer) MinimumLevel");
    }

    [Fact]
    public async Task LiveHit_PlainMembership_DeniedByHigherMinimumLevel()
    {
        await SeedLinkedUserAsync();
        SetHttpContextRoute(GuildId);

        var live = new GuildAccessHandler.LiveGuildMembershipResult(
            IsAdministrator: false, GuildName: "Live Guild", GuildIconHash: null,
            IsOwner: false, PermissionsRaw: 0);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Moderator);
        var handler = CreateHandler(live);
        var context = CreateContext(principal, new GuildAccessRequirement(GuildAccessLevel.Moderator));

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse(
            "plain live membership only satisfies the lowest (Viewer) MinimumLevel");
    }

    [Fact]
    public async Task CacheHit_PlainMembership_DefaultViewerPolicy_StillSucceeds()
    {
        // Behaviour must be unchanged for the only policy in use today (MinimumLevel = Viewer).
        await SeedLinkedUserAsync();
        await SeedCachedMembershipAsync(GuildId, administrator: false);
        SetHttpContextRoute(GuildId);

        var principal = CreatePrincipal(UserId, IdentitySeeder.Roles.Moderator);
        var handler = CreateHandler();
        var context = CreateContext(principal, new GuildAccessRequirement(GuildAccessLevel.Viewer));

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------

    private async Task SeedCachedMembershipAsync(
        ulong guildId, bool administrator = false, DateTime? lastUpdatedAt = null)
    {
        var membership = new UserDiscordGuild
        {
            Id = Guid.NewGuid(),
            ApplicationUserId = UserId,
            GuildId = guildId,
            GuildName = "Cached Guild",
            IsOwner = false,
            Permissions = administrator ? 8L : 0L, // 0x8 = ADMINISTRATOR
            CapturedAt = lastUpdatedAt ?? DateTime.UtcNow,
            LastUpdatedAt = lastUpdatedAt ?? DateTime.UtcNow
        };
        _dbContext.Set<UserDiscordGuild>().Add(membership);
        await _dbContext.SaveChangesAsync();

        _mockUserDiscordGuildService
            .Setup(s => s.GetUserGuildsAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserDiscordGuild> { membership }.AsReadOnly());
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Test double that overrides the live-Discord-gateway seam with a canned result, since
    /// Discord.Net's <c>SocketGuild</c>/<c>SocketGuildUser</c> are sealed and cannot be mocked.
    /// </summary>
    private sealed class TestableGuildAccessHandler : GuildAccessHandler
    {
        private readonly LiveGuildMembershipResult? _liveResult;

        public TestableGuildAccessHandler(
            UserManager<ApplicationUser> userManager,
            DiscordSocketClient discordClient,
            BotDbContext dbContext,
            IUserDiscordGuildService userDiscordGuildService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<GuildAccessHandler> logger,
            IOptions<GuildMembershipCacheOptions> cacheOptions,
            LiveGuildMembershipResult? liveResult)
            : base(userManager, discordClient, dbContext, userDiscordGuildService, httpContextAccessor, logger, cacheOptions)
        {
            _liveResult = liveResult;
        }

        protected override Task<LiveGuildMembershipResult?> GetLiveGuildMembershipAsync(ulong guildId, ulong discordUserId)
            => Task.FromResult(_liveResult);
    }
}
