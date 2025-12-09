using DiscordBot.Core.Entities;
using FluentAssertions;

namespace DiscordBot.Tests.Core.Entities;

/// <summary>
/// Unit tests for UserGuildAccess entity.
/// </summary>
public class UserGuildAccessTests
{
    [Fact]
    public void Constructor_SetsDefaultAccessLevel()
    {
        // Arrange & Act
        var access = new UserGuildAccess();

        // Assert
        access.AccessLevel.Should().Be(GuildAccessLevel.Viewer,
            "newly created UserGuildAccess should default to Viewer level");
    }

    [Fact]
    public void Constructor_SetsGrantedAtToUtcNow()
    {
        // Arrange & Act
        var access = new UserGuildAccess();

        // Assert
        access.GrantedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1),
            "GrantedAt should be set to current UTC time");
    }

    [Fact]
    public void ApplicationUserId_CanBeSet()
    {
        // Arrange
        var access = new UserGuildAccess();
        const string expectedUserId = "user123";

        // Act
        access.ApplicationUserId = expectedUserId;

        // Assert
        access.ApplicationUserId.Should().Be(expectedUserId);
    }

    [Fact]
    public void GuildId_CanBeSet()
    {
        // Arrange
        var access = new UserGuildAccess();
        const ulong expectedGuildId = 123456789012345678;

        // Act
        access.GuildId = expectedGuildId;

        // Assert
        access.GuildId.Should().Be(expectedGuildId);
    }

    [Fact]
    public void AccessLevel_CanBeSetToViewer()
    {
        // Arrange
        var access = new UserGuildAccess();

        // Act
        access.AccessLevel = GuildAccessLevel.Viewer;

        // Assert
        access.AccessLevel.Should().Be(GuildAccessLevel.Viewer);
    }

    [Fact]
    public void AccessLevel_CanBeSetToModerator()
    {
        // Arrange
        var access = new UserGuildAccess();

        // Act
        access.AccessLevel = GuildAccessLevel.Moderator;

        // Assert
        access.AccessLevel.Should().Be(GuildAccessLevel.Moderator);
    }

    [Fact]
    public void AccessLevel_CanBeSetToAdmin()
    {
        // Arrange
        var access = new UserGuildAccess();

        // Act
        access.AccessLevel = GuildAccessLevel.Admin;

        // Assert
        access.AccessLevel.Should().Be(GuildAccessLevel.Admin);
    }

    [Fact]
    public void AccessLevel_CanBeSetToOwner()
    {
        // Arrange
        var access = new UserGuildAccess();

        // Act
        access.AccessLevel = GuildAccessLevel.Owner;

        // Assert
        access.AccessLevel.Should().Be(GuildAccessLevel.Owner);
    }

    [Fact]
    public void GrantedAt_CanBeSetToSpecificDate()
    {
        // Arrange
        var access = new UserGuildAccess();
        var expectedDate = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        access.GrantedAt = expectedDate;

        // Assert
        access.GrantedAt.Should().Be(expectedDate);
    }

    [Fact]
    public void GrantedByUserId_DefaultsToNull()
    {
        // Arrange & Act
        var access = new UserGuildAccess();

        // Assert
        access.GrantedByUserId.Should().BeNull(
            "GrantedByUserId should be null for system-granted access");
    }

    [Fact]
    public void GrantedByUserId_CanBeSet()
    {
        // Arrange
        var access = new UserGuildAccess();
        const string expectedGranterId = "granter123";

        // Act
        access.GrantedByUserId = expectedGranterId;

        // Assert
        access.GrantedByUserId.Should().Be(expectedGranterId);
    }

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        // Arrange
        const string userId = "user123";
        const ulong guildId = 987654321098765432;
        const GuildAccessLevel accessLevel = GuildAccessLevel.Admin;
        var grantedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        const string grantedBy = "granter456";

        // Act
        var access = new UserGuildAccess
        {
            ApplicationUserId = userId,
            GuildId = guildId,
            AccessLevel = accessLevel,
            GrantedAt = grantedAt,
            GrantedByUserId = grantedBy
        };

        // Assert
        access.ApplicationUserId.Should().Be(userId);
        access.GuildId.Should().Be(guildId);
        access.AccessLevel.Should().Be(accessLevel);
        access.GrantedAt.Should().Be(grantedAt);
        access.GrantedByUserId.Should().Be(grantedBy);
    }

    [Theory]
    [InlineData(GuildAccessLevel.Viewer, 0)]
    [InlineData(GuildAccessLevel.Moderator, 1)]
    [InlineData(GuildAccessLevel.Admin, 2)]
    [InlineData(GuildAccessLevel.Owner, 3)]
    public void GuildAccessLevel_HasExpectedNumericValue(GuildAccessLevel level, int expectedValue)
    {
        // Arrange & Act
        var numericValue = (int)level;

        // Assert
        numericValue.Should().Be(expectedValue,
            $"{level} should have numeric value {expectedValue}");
    }

    [Theory]
    [InlineData(GuildAccessLevel.Viewer, GuildAccessLevel.Moderator, true)]
    [InlineData(GuildAccessLevel.Moderator, GuildAccessLevel.Admin, true)]
    [InlineData(GuildAccessLevel.Admin, GuildAccessLevel.Owner, true)]
    [InlineData(GuildAccessLevel.Admin, GuildAccessLevel.Moderator, false)]
    [InlineData(GuildAccessLevel.Moderator, GuildAccessLevel.Viewer, false)]
    [InlineData(GuildAccessLevel.Owner, GuildAccessLevel.Admin, false)]
    [InlineData(GuildAccessLevel.Viewer, GuildAccessLevel.Viewer, false)]
    public void GuildAccessLevel_Comparison_WorksCorrectly(
        GuildAccessLevel level1,
        GuildAccessLevel level2,
        bool expectedLessThan)
    {
        // Arrange & Act
        var isLessThan = level1 < level2;

        // Assert
        isLessThan.Should().Be(expectedLessThan,
            $"{level1} < {level2} should be {expectedLessThan}");
    }

    [Theory]
    [InlineData(GuildAccessLevel.Viewer, GuildAccessLevel.Viewer, true)]
    [InlineData(GuildAccessLevel.Moderator, GuildAccessLevel.Viewer, true)]
    [InlineData(GuildAccessLevel.Admin, GuildAccessLevel.Moderator, true)]
    [InlineData(GuildAccessLevel.Owner, GuildAccessLevel.Admin, true)]
    [InlineData(GuildAccessLevel.Viewer, GuildAccessLevel.Moderator, false)]
    [InlineData(GuildAccessLevel.Moderator, GuildAccessLevel.Admin, false)]
    [InlineData(GuildAccessLevel.Admin, GuildAccessLevel.Owner, false)]
    public void GuildAccessLevel_GreaterThanOrEqual_WorksForAuthorizationChecks(
        GuildAccessLevel userLevel,
        GuildAccessLevel requiredLevel,
        bool shouldHaveAccess)
    {
        // Arrange & Act
        var hasAccess = userLevel >= requiredLevel;

        // Assert
        hasAccess.Should().Be(shouldHaveAccess,
            $"user with {userLevel} should {(shouldHaveAccess ? "" : "not ")}have access requiring {requiredLevel}");
    }

    [Fact]
    public void GuildAccessLevel_ViewerIsLowestLevel()
    {
        // Arrange
        var allLevels = Enum.GetValues<GuildAccessLevel>();

        // Act
        var lowestLevel = allLevels.Min();

        // Assert
        lowestLevel.Should().Be(GuildAccessLevel.Viewer,
            "Viewer should be the lowest access level");
    }

    [Fact]
    public void GuildAccessLevel_OwnerIsHighestLevel()
    {
        // Arrange
        var allLevels = Enum.GetValues<GuildAccessLevel>();

        // Act
        var highestLevel = allLevels.Max();

        // Assert
        highestLevel.Should().Be(GuildAccessLevel.Owner,
            "Owner should be the highest access level");
    }

    [Fact]
    public void GuildAccessLevel_HasExpectedNumberOfLevels()
    {
        // Arrange & Act
        var allLevels = Enum.GetValues<GuildAccessLevel>();

        // Assert
        allLevels.Should().HaveCount(4, "there should be exactly 4 access levels");
    }

    [Fact]
    public void GuildAccessLevel_AllLevelsAreSequential()
    {
        // Arrange
        var allLevels = Enum.GetValues<GuildAccessLevel>().OrderBy(l => l).ToArray();

        // Act & Assert
        for (int i = 0; i < allLevels.Length; i++)
        {
            ((int)allLevels[i]).Should().Be(i,
                $"access level at index {i} should have numeric value {i}");
        }
    }
}
