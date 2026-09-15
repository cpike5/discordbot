using System.Security.Claims;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.Core.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Bot.Blazor;

/// <summary>
/// Unit tests for <see cref="RevalidatingIdentityAuthenticationStateProvider"/>. Exercises the
/// testable core (<see cref="RevalidatingIdentityAuthenticationStateProvider.ValidateUserAsync"/>)
/// directly against a mocked <see cref="UserManager{TUser}"/>, the same technique
/// <c>GuildAccessHandlerTests</c> uses, rather than going through <c>ValidateAuthenticationStateAsync</c>
/// and a real DI scope.
/// </summary>
public class RevalidatingIdentityAuthenticationStateProviderTests
{
    private const string UserId = "user-1";
    private static readonly string StampClaimType = new IdentityOptions().ClaimsIdentity.SecurityStampClaimType;

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
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

    private static RevalidatingIdentityAuthenticationStateProvider CreateProvider()
        => new(
            NullLoggerFactory.Instance,
            Mock.Of<IServiceScopeFactory>(),
            Options.Create(new IdentityOptions()));

    private static ClaimsPrincipal CreatePrincipal(string userId, string? securityStamp = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        if (securityStamp is not null)
        {
            claims.Add(new Claim(StampClaimType, securityStamp));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    [Fact]
    public async Task ValidUser_NotLockedOut_NoStampSupport_ReturnsTrue()
    {
        var manager = CreateUserManagerMock();
        var user = new ApplicationUser { Id = UserId, UserName = "u@test.com", Email = "u@test.com" };
        manager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        manager.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        manager.Setup(m => m.SupportsUserSecurityStamp).Returns(false);

        var provider = CreateProvider();

        var result = await provider.ValidateUserAsync(manager.Object, CreatePrincipal(UserId));

        result.Should().BeTrue("the user exists, isn't locked out, and the store doesn't support stamps");
    }

    [Fact]
    public async Task MissingUser_ReturnsFalse()
    {
        var manager = CreateUserManagerMock();
        manager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync((ApplicationUser?)null);

        var provider = CreateProvider();

        var result = await provider.ValidateUserAsync(manager.Object, CreatePrincipal(UserId));

        result.Should().BeFalse("a deleted/missing user must fail revalidation");
    }

    [Fact]
    public async Task LockedOutUser_ReturnsFalse()
    {
        var manager = CreateUserManagerMock();
        var user = new ApplicationUser { Id = UserId, UserName = "u@test.com", Email = "u@test.com" };
        manager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        manager.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(true);

        var provider = CreateProvider();

        var result = await provider.ValidateUserAsync(manager.Object, CreatePrincipal(UserId));

        result.Should().BeFalse("a locked-out (banned) user must fail revalidation even though the account still exists");
    }

    [Fact]
    public async Task SecurityStampMismatch_ReturnsFalse()
    {
        var manager = CreateUserManagerMock();
        var user = new ApplicationUser { Id = UserId, UserName = "u@test.com", Email = "u@test.com" };
        manager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        manager.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        manager.Setup(m => m.SupportsUserSecurityStamp).Returns(true);
        manager.Setup(m => m.GetSecurityStampAsync(user)).ReturnsAsync("current-stamp");

        var provider = CreateProvider();
        var principal = CreatePrincipal(UserId, securityStamp: "stale-stamp");

        var result = await provider.ValidateUserAsync(manager.Object, principal);

        result.Should().BeFalse("a stale security stamp claim (password/role change since sign-in) must fail revalidation");
    }

    [Fact]
    public async Task SecurityStampMatches_ReturnsTrue()
    {
        var manager = CreateUserManagerMock();
        var user = new ApplicationUser { Id = UserId, UserName = "u@test.com", Email = "u@test.com" };
        manager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        manager.Setup(m => m.IsLockedOutAsync(user)).ReturnsAsync(false);
        manager.Setup(m => m.SupportsUserSecurityStamp).Returns(true);
        manager.Setup(m => m.GetSecurityStampAsync(user)).ReturnsAsync("current-stamp");

        var provider = CreateProvider();
        var principal = CreatePrincipal(UserId, securityStamp: "current-stamp");

        var result = await provider.ValidateUserAsync(manager.Object, principal);

        result.Should().BeTrue();
    }
}
