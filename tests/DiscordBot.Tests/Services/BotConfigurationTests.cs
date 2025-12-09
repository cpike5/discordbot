using DiscordBot.Bot.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="BotConfiguration"/>.
/// </summary>
public class BotConfigurationTests
{
    [Fact]
    public void Constructor_ShouldSetDefaultValues()
    {
        // Arrange & Act
        var config = new BotConfiguration();

        // Assert
        config.Token.Should().Be(string.Empty, "default token should be empty string");
        config.TestGuildId.Should().BeNull("TestGuildId should be null by default");
    }

    [Fact]
    public void SectionName_ShouldBeDiscord()
    {
        // Arrange & Act
        var sectionName = BotConfiguration.SectionName;

        // Assert
        sectionName.Should().Be("Discord", "section name should match appsettings structure");
    }

    [Fact]
    public void Token_ShouldBeSettable()
    {
        // Arrange
        var config = new BotConfiguration();
        var expectedToken = "test-token-value";

        // Act
        config.Token = expectedToken;

        // Assert
        config.Token.Should().Be(expectedToken, "Token property should be settable");
    }

    [Fact]
    public void TestGuildId_ShouldBeSettable()
    {
        // Arrange
        var config = new BotConfiguration();
        ulong expectedGuildId = 123456789012345678;

        // Act
        config.TestGuildId = expectedGuildId;

        // Assert
        config.TestGuildId.Should().Be(expectedGuildId, "TestGuildId property should be settable");
    }

    [Fact]
    public void Bind_ShouldPopulateFromConfiguration()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Discord:Token", "my-bot-token" },
            { "Discord:TestGuildId", "987654321098765432" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var botConfig = new BotConfiguration();

        // Act
        configuration.GetSection(BotConfiguration.SectionName).Bind(botConfig);

        // Assert
        botConfig.Token.Should().Be("my-bot-token", "Token should be bound from configuration");
        botConfig.TestGuildId.Should().Be(987654321098765432, "TestGuildId should be bound from configuration");
    }

    [Fact]
    public void Bind_WithMissingTestGuildId_ShouldLeaveItNull()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Discord:Token", "my-bot-token" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var botConfig = new BotConfiguration();

        // Act
        configuration.GetSection(BotConfiguration.SectionName).Bind(botConfig);

        // Assert
        botConfig.Token.Should().Be("my-bot-token", "Token should be bound from configuration");
        botConfig.TestGuildId.Should().BeNull("TestGuildId should remain null when not in configuration");
    }

    [Fact]
    public void Bind_WithEmptyToken_ShouldSetEmptyString()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "Discord:Token", "" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var botConfig = new BotConfiguration();

        // Act
        configuration.GetSection(BotConfiguration.SectionName).Bind(botConfig);

        // Assert
        botConfig.Token.Should().Be(string.Empty, "Token should be empty string when configured as empty");
    }
}
