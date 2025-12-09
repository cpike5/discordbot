using DiscordBot.Bot.Authorization;
using DiscordBot.Core.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;

namespace DiscordBot.Tests.Bot.Authorization;

/// <summary>
/// Unit tests for GuildAccessRequirement.
/// </summary>
public class GuildAccessRequirementTests
{
    [Fact]
    public void Constructor_WithDefaults_UsesViewerLevelAndGuildId()
    {
        // Act
        var requirement = new GuildAccessRequirement();

        // Assert
        requirement.MinimumLevel.Should().Be(GuildAccessLevel.Viewer);
        requirement.GuildIdParameterName.Should().Be("guildId");
    }

    [Fact]
    public void Constructor_WithCustomMinimumLevel_UsesSpecifiedLevel()
    {
        // Act
        var requirement = new GuildAccessRequirement(GuildAccessLevel.Admin);

        // Assert
        requirement.MinimumLevel.Should().Be(GuildAccessLevel.Admin);
        requirement.GuildIdParameterName.Should().Be("guildId");
    }

    [Fact]
    public void Constructor_WithCustomParameters_UsesCustomValues()
    {
        // Arrange
        const string customParamName = "serverId";

        // Act
        var requirement = new GuildAccessRequirement(GuildAccessLevel.Moderator, customParamName);

        // Assert
        requirement.MinimumLevel.Should().Be(GuildAccessLevel.Moderator);
        requirement.GuildIdParameterName.Should().Be(customParamName);
    }

    [Theory]
    [InlineData(GuildAccessLevel.Viewer)]
    [InlineData(GuildAccessLevel.Moderator)]
    [InlineData(GuildAccessLevel.Admin)]
    [InlineData(GuildAccessLevel.Owner)]
    public void Constructor_AcceptsAllAccessLevels(GuildAccessLevel level)
    {
        // Act
        var requirement = new GuildAccessRequirement(level);

        // Assert
        requirement.MinimumLevel.Should().Be(level);
    }

    [Fact]
    public void Requirement_ImplementsIAuthorizationRequirement()
    {
        // Act
        var requirement = new GuildAccessRequirement();

        // Assert
        requirement.Should().BeAssignableTo<IAuthorizationRequirement>();
    }
}
