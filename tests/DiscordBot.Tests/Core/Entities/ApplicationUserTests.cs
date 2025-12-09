using DiscordBot.Core.Entities;
using FluentAssertions;

namespace DiscordBot.Tests.Core.Entities;

/// <summary>
/// Unit tests for ApplicationUser entity.
/// </summary>
public class ApplicationUserTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var user = new ApplicationUser();

        // Assert
        user.IsActive.Should().BeTrue("newly created users should be active by default");
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1),
            "CreatedAt should be set to current UTC time");
    }

    [Fact]
    public void DiscordUserId_CanBeNull()
    {
        // Arrange
        var user = new ApplicationUser
        {
            UserName = "testuser@example.com",
            Email = "testuser@example.com"
        };

        // Act
        user.DiscordUserId = null;

        // Assert
        user.DiscordUserId.Should().BeNull("users who haven't linked Discord should have null DiscordUserId");
    }

    [Fact]
    public void DiscordUserId_CanBeSet()
    {
        // Arrange
        var user = new ApplicationUser();
        const ulong expectedDiscordId = 123456789012345678;

        // Act
        user.DiscordUserId = expectedDiscordId;

        // Assert
        user.DiscordUserId.Should().Be(expectedDiscordId);
    }

    [Fact]
    public void DiscordUsername_CanBeNull()
    {
        // Arrange
        var user = new ApplicationUser();

        // Act
        user.DiscordUsername = null;

        // Assert
        user.DiscordUsername.Should().BeNull("users without Discord should have null username");
    }

    [Fact]
    public void DiscordUsername_CanBeSet()
    {
        // Arrange
        var user = new ApplicationUser();
        const string expectedUsername = "testuser#1234";

        // Act
        user.DiscordUsername = expectedUsername;

        // Assert
        user.DiscordUsername.Should().Be(expectedUsername);
    }

    [Fact]
    public void DiscordAvatarUrl_CanBeNull()
    {
        // Arrange
        var user = new ApplicationUser();

        // Act
        user.DiscordAvatarUrl = null;

        // Assert
        user.DiscordAvatarUrl.Should().BeNull();
    }

    [Fact]
    public void DiscordAvatarUrl_CanBeSet()
    {
        // Arrange
        var user = new ApplicationUser();
        const string expectedUrl = "https://cdn.discordapp.com/avatars/123456789012345678/abcdef123456.png";

        // Act
        user.DiscordAvatarUrl = expectedUrl;

        // Assert
        user.DiscordAvatarUrl.Should().Be(expectedUrl);
    }

    [Fact]
    public void DisplayName_CanBeNull()
    {
        // Arrange
        var user = new ApplicationUser();

        // Act
        user.DisplayName = null;

        // Assert
        user.DisplayName.Should().BeNull();
    }

    [Fact]
    public void DisplayName_CanBeSet()
    {
        // Arrange
        var user = new ApplicationUser();
        const string expectedDisplayName = "Test User";

        // Act
        user.DisplayName = expectedDisplayName;

        // Assert
        user.DisplayName.Should().Be(expectedDisplayName);
    }

    [Fact]
    public void IsActive_CanBeSetToFalse()
    {
        // Arrange
        var user = new ApplicationUser();

        // Act
        user.IsActive = false;

        // Assert
        user.IsActive.Should().BeFalse("users can be deactivated");
    }

    [Fact]
    public void CreatedAt_CanBeSetToSpecificDate()
    {
        // Arrange
        var user = new ApplicationUser();
        var expectedDate = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        user.CreatedAt = expectedDate;

        // Assert
        user.CreatedAt.Should().Be(expectedDate);
    }

    [Fact]
    public void LastLoginAt_DefaultsToNull()
    {
        // Arrange & Act
        var user = new ApplicationUser();

        // Assert
        user.LastLoginAt.Should().BeNull("users have not logged in when first created");
    }

    [Fact]
    public void LastLoginAt_CanBeSet()
    {
        // Arrange
        var user = new ApplicationUser();
        var expectedLoginTime = DateTime.UtcNow;

        // Act
        user.LastLoginAt = expectedLoginTime;

        // Assert
        user.LastLoginAt.Should().BeCloseTo(expectedLoginTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void InheritsFromIdentityUser()
    {
        // Arrange & Act
        var user = new ApplicationUser();

        // Assert
        user.Should().BeAssignableTo<Microsoft.AspNetCore.Identity.IdentityUser>(
            "ApplicationUser should extend IdentityUser");
    }

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        // Arrange
        const ulong discordId = 123456789012345678;
        const string discordUsername = "testuser#1234";
        const string discordAvatarUrl = "https://cdn.discordapp.com/avatars/123456789012345678/abcdef123456.png";
        const string displayName = "Test User";
        const string email = "testuser@example.com";
        const string userName = "testuser@example.com";
        var createdAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var lastLoginAt = new DateTime(2024, 1, 2, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var user = new ApplicationUser
        {
            DiscordUserId = discordId,
            DiscordUsername = discordUsername,
            DiscordAvatarUrl = discordAvatarUrl,
            DisplayName = displayName,
            Email = email,
            UserName = userName,
            IsActive = true,
            CreatedAt = createdAt,
            LastLoginAt = lastLoginAt
        };

        // Assert
        user.DiscordUserId.Should().Be(discordId);
        user.DiscordUsername.Should().Be(discordUsername);
        user.DiscordAvatarUrl.Should().Be(discordAvatarUrl);
        user.DisplayName.Should().Be(displayName);
        user.Email.Should().Be(email);
        user.UserName.Should().Be(userName);
        user.IsActive.Should().BeTrue();
        user.CreatedAt.Should().Be(createdAt);
        user.LastLoginAt.Should().Be(lastLoginAt);
    }
}
