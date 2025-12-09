using DiscordBot.Bot.Authorization;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Authorization;

/// <summary>
/// Unit tests for GuildAccessRequirement.
/// </summary>
public class GuildAccessRequirementTests
{
    [Fact]
    public void Constructor_WithDefaultParameter_UsesGuildId()
    {
        // Act
        var requirement = new GuildAccessRequirement();

        // Assert
        requirement.GuildIdParameterName.Should().Be("guildId");
    }

    [Fact]
    public void Constructor_WithCustomParameter_UsesCustomValue()
    {
        // Arrange
        const string customParamName = "serverId";

        // Act
        var requirement = new GuildAccessRequirement(customParamName);

        // Assert
        requirement.GuildIdParameterName.Should().Be(customParamName);
    }

    [Theory]
    [InlineData("guildId")]
    [InlineData("serverId")]
    [InlineData("id")]
    [InlineData("guild")]
    public void Constructor_AcceptsVariousParameterNames(string parameterName)
    {
        // Act
        var requirement = new GuildAccessRequirement(parameterName);

        // Assert
        requirement.GuildIdParameterName.Should().Be(parameterName);
    }
}
