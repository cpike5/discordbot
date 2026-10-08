using System.Security.Claims;
using Discord.WebSocket;
using DiscordBot.Bot.Authorization;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Authorization;

/// <summary>
/// Unit tests for <see cref="GuildAccessHandler"/>, the handler registered for
/// <see cref="GuildAccessRequirement"/>. These cover the checks that decide before the Discord
/// client is consulted for guild membership.
/// </summary>
public class GuildAccessHandlerTests : IDisposable
{
    private const ulong GuildId = 123456789012345678;

    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly DiscordSocketClient _discordClient;
    private readonly GuildAccessHandler _handler;

    public GuildAccessHandlerTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _discordClient = new DiscordSocketClient();

        _handler = new GuildAccessHandler(
            _mockUserManager.Object,
            _discordClient,
            _mockHttpContextAccessor.Object,
            Mock.Of<ILogger<GuildAccessHandler>>());
    }

    [Fact]
    public async Task HandleRequirementAsync_SuperAdmin_Succeeds()
    {
        var context = CreateContext(CreatePrincipal(IdentitySeeder.Roles.SuperAdmin));

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue("SuperAdmin should have access to all guilds");
        context.HasFailed.Should().BeFalse();
    }

    [Fact]
    public async Task HandleRequirementAsync_NoHttpContext_DoesNotSucceed()
    {
        _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var context = CreateContext(CreatePrincipal(IdentitySeeder.Roles.Moderator));

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("authorization should not succeed without an HTTP context");
    }

    [Fact]
    public async Task HandleRequirementAsync_NoGuildIdInRoute_DoesNotSucceed()
    {
        SetupHttpContext(new RouteValueDictionary());
        var context = CreateContext(CreatePrincipal(IdentitySeeder.Roles.Moderator));

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("authorization should not succeed without a guildId");
    }

    [Fact]
    public async Task HandleRequirementAsync_GuildIdOnlyInQueryString_DoesNotSucceed()
    {
        var httpContext = SetupHttpContext(new RouteValueDictionary());
        httpContext.Request.QueryString = new QueryString($"?guildId={GuildId}");
        var context = CreateContext(CreatePrincipal(IdentitySeeder.Roles.Moderator));

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse(
            "a caller-controlled query-string guildId must never decide guild access");
        _mockUserManager.Verify(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Never);
    }

    [Fact]
    public async Task HandleRequirementAsync_InvalidGuildId_DoesNotSucceed()
    {
        SetupHttpContext(new RouteValueDictionary { ["guildId"] = "not-a-number" });
        var context = CreateContext(CreatePrincipal(IdentitySeeder.Roles.Moderator));

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("authorization should not succeed with an invalid guildId");
    }

    [Fact]
    public async Task HandleRequirementAsync_UserNotFound_DoesNotSucceed()
    {
        SetupHttpContext(new RouteValueDictionary { ["guildId"] = GuildId.ToString() });
        _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);
        var context = CreateContext(CreatePrincipal(IdentitySeeder.Roles.Moderator));

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("authorization should not succeed when the user is not found");
    }

    [Fact]
    public async Task HandleRequirementAsync_UserWithoutDiscordLinked_DoesNotSucceed()
    {
        SetupHttpContext(new RouteValueDictionary { ["guildId"] = GuildId.ToString() });
        _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { Id = "user123", DiscordUserId = null });
        var context = CreateContext(CreatePrincipal(IdentitySeeder.Roles.Moderator));

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse("guild access requires a linked Discord account");
    }

    [Fact]
    public async Task HandleRequirementAsync_GuildNotKnownToDiscordClient_DoesNotSucceed()
    {
        SetupHttpContext(new RouteValueDictionary { ["guildId"] = GuildId.ToString() });
        _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(new ApplicationUser { Id = "user123", DiscordUserId = 987654321UL });
        var context = CreateContext(CreatePrincipal(IdentitySeeder.Roles.Moderator));

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse(
            "authorization should not succeed for a guild the bot is not connected to");
    }

    [Fact]
    public async Task HandleRequirementAsync_GuildIdFromResource_IsUsedWithoutHttpContext()
    {
        _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        _mockUserManager.Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);
        var context = CreateContext(CreatePrincipal(IdentitySeeder.Roles.Moderator), resource: GuildId);

        await _handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
        _mockUserManager.Verify(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Once,
            "a guild id passed as the resource should be checked even without an HTTP context");
    }

    public void Dispose()
    {
        _discordClient.Dispose();
    }

    private static ClaimsPrincipal CreatePrincipal(string role)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user123"),
            new(ClaimTypes.Role, role)
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal user, object? resource = null)
    {
        var requirement = new GuildAccessRequirement(GuildAccessLevel.Viewer);
        return new AuthorizationHandlerContext(new[] { requirement }, user, resource);
    }

    private DefaultHttpContext SetupHttpContext(RouteValueDictionary routeValues)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = routeValues;
        _mockHttpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);
        return httpContext;
    }
}
