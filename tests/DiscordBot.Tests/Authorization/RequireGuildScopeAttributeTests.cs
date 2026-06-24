using System.Security.Claims;
using DiscordBot.Bot.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.Tests.Authorization;

/// <summary>
/// Unit tests for <see cref="RequireGuildScopeAttribute"/> — the filter that closes the
/// cross-guild IDOR by enforcing per-guild access when a guild ID is present and restricting
/// global/cross-guild views to elevated roles.
/// </summary>
public class RequireGuildScopeAttributeTests
{
    private const string GuildAccessPolicy = "GuildAccess";

    [Fact]
    public async Task UnauthenticatedUser_IsChallenged()
    {
        var authz = new Mock<IAuthorizationService>();
        var context = CreateContext(new ClaimsPrincipal(new ClaimsIdentity()), authz.Object, routeGuildId: "123");

        await new RequireGuildScopeAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ChallengeResult>();
    }

    [Fact]
    public async Task NoGuildId_NonElevatedRole_IsForbidden()
    {
        var authz = new Mock<IAuthorizationService>();
        var context = CreateContext(AuthenticatedUser("Viewer"), authz.Object);

        await new RequireGuildScopeAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>("global views must require an elevated role");
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    public async Task NoGuildId_ElevatedRole_IsAllowed(string role)
    {
        var authz = new Mock<IAuthorizationService>();
        var context = CreateContext(AuthenticatedUser(role), authz.Object);

        await new RequireGuildScopeAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task GuildIdInRoute_GuildAccessGranted_IsAllowed()
    {
        var authz = new Mock<IAuthorizationService>();
        authz.Setup(s => s.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), GuildAccessPolicy))
            .ReturnsAsync(AuthorizationResult.Success());
        var context = CreateContext(AuthenticatedUser("Viewer"), authz.Object, routeGuildId: "123");

        await new RequireGuildScopeAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeNull();
        authz.Verify(s => s.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), GuildAccessPolicy), Times.Once);
    }

    [Fact]
    public async Task GuildIdInQuery_GuildAccessDenied_IsForbidden()
    {
        var authz = new Mock<IAuthorizationService>();
        authz.Setup(s => s.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), GuildAccessPolicy))
            .ReturnsAsync(AuthorizationResult.Failed());
        var context = CreateContext(AuthenticatedUser("Moderator"), authz.Object, queryGuildId: "999");

        await new RequireGuildScopeAttribute().OnAuthorizationAsync(context);

        context.Result.Should().BeOfType<ForbidResult>("a user without access to the requested guild must be denied");
    }

    private static AuthorizationFilterContext CreateContext(
        ClaimsPrincipal user,
        IAuthorizationService authorizationService,
        string? routeGuildId = null,
        string? queryGuildId = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(authorizationService);

        var httpContext = new DefaultHttpContext
        {
            User = user,
            RequestServices = services.BuildServiceProvider()
        };

        if (routeGuildId != null)
        {
            httpContext.Request.RouteValues["guildId"] = routeGuildId;
        }

        if (queryGuildId != null)
        {
            httpContext.Request.QueryString = new QueryString($"?guildId={queryGuildId}");
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static ClaimsPrincipal AuthenticatedUser(params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-1") };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        return new ClaimsPrincipal(identity);
    }
}
