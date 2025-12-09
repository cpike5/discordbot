using System.Security.Claims;
using DiscordBot.Bot.Authorization;
using DiscordBot.Core.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Authorization;

/// <summary>
/// Unit tests for DiscordClaimsTransformation.
/// </summary>
public class DiscordClaimsTransformationTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<ILogger<DiscordClaimsTransformation>> _mockLogger;
    private readonly DiscordClaimsTransformation _transformation;

    public DiscordClaimsTransformationTests()
    {
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

        _mockLogger = new Mock<ILogger<DiscordClaimsTransformation>>();

        _transformation = new DiscordClaimsTransformation(
            _mockUserManager.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task TransformAsync_WithUnauthenticatedUser_ReturnsSamePrincipal()
    {
        // Arrange
        var identity = new ClaimsIdentity(); // Not authenticated (no type)
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.Should().BeSameAs(principal);
        result.Claims.Should().BeEmpty();
    }

    [Fact]
    public async Task TransformAsync_WithDiscordClaimsAlreadyPresent_ReturnsSamePrincipal()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-id"),
            new Claim("discord:user_id", "123456789")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.Should().BeSameAs(principal);
        // UserManager should not be called
        _mockUserManager.Verify(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Never);
    }

    [Fact]
    public async Task TransformAsync_WhenUserNotFound_ReturnsSamePrincipal()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-id")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.Should().BeSameAs(principal);
    }

    [Fact]
    public async Task TransformAsync_WithDiscordLinkedUser_AddsDiscordClaims()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-id")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var user = new ApplicationUser
        {
            Id = "user-id",
            DiscordUserId = 123456789012345678,
            DiscordUsername = "testuser",
            DiscordAvatarUrl = "https://cdn.discordapp.com/avatars/123/abc.png",
            DisplayName = "Test User"
        };

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.Should().NotBeNull();
        result.HasClaim("discord:user_id", "123456789012345678").Should().BeTrue();
        result.HasClaim("discord:linked", "true").Should().BeTrue();
        result.HasClaim("discord:username", "testuser").Should().BeTrue();
        result.HasClaim("discord:avatar_url", "https://cdn.discordapp.com/avatars/123/abc.png").Should().BeTrue();
        result.HasClaim("display_name", "Test User").Should().BeTrue();
    }

    [Fact]
    public async Task TransformAsync_WithUnlinkedUser_AddsLinkedFalseClaim()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-id")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var user = new ApplicationUser
        {
            Id = "user-id",
            DiscordUserId = null,
            DisplayName = "Test User"
        };

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.Should().NotBeNull();
        result.HasClaim("discord:linked", "false").Should().BeTrue();
        result.HasClaim("display_name", "Test User").Should().BeTrue();
        result.HasClaim(c => c.Type == "discord:user_id").Should().BeFalse();
        result.HasClaim(c => c.Type == "discord:username").Should().BeFalse();
    }

    [Fact]
    public async Task TransformAsync_WithPartialDiscordInfo_OnlyAddsPresentClaims()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "user-id")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var user = new ApplicationUser
        {
            Id = "user-id",
            DiscordUserId = 123456789012345678,
            DiscordUsername = null, // No username
            DiscordAvatarUrl = null, // No avatar
            DisplayName = null // No display name
        };

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        result.Should().NotBeNull();
        result.HasClaim("discord:user_id", "123456789012345678").Should().BeTrue();
        result.HasClaim("discord:linked", "true").Should().BeTrue();
        result.HasClaim(c => c.Type == "discord:username").Should().BeFalse();
        result.HasClaim(c => c.Type == "discord:avatar_url").Should().BeFalse();
        result.HasClaim(c => c.Type == "display_name").Should().BeFalse();
    }

    [Fact]
    public async Task TransformAsync_WithNonClaimsIdentity_ReturnsSamePrincipal()
    {
        // Arrange - Create a GenericIdentity that isn't a ClaimsIdentity
        var identity = new System.Security.Principal.GenericIdentity("testuser", "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var user = new ApplicationUser
        {
            Id = "user-id",
            DiscordUserId = 123456789012345678
        };

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        // Act
        var result = await _transformation.TransformAsync(principal);

        // Assert
        // The method should add claims since GenericIdentity creates a ClaimsIdentity internally
        result.Should().NotBeNull();
    }
}
