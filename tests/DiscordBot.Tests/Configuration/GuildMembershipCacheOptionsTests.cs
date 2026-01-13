using DiscordBot.Core.Configuration;
using FluentAssertions;

namespace DiscordBot.Tests.Configuration;

/// <summary>
/// Unit tests for GuildMembershipCacheOptions.
/// </summary>
public class GuildMembershipCacheOptionsTests
{
    [Fact]
    public void SectionName_HasCorrectValue()
    {
        // Assert
        GuildMembershipCacheOptions.SectionName.Should().Be("GuildMembershipCache");
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange
        var options = new GuildMembershipCacheOptions();

        // Assert
        options.StoredGuildMembershipDurationMinutes.Should().Be(30, "stored guild membership data should be cached for 30 minutes by default");
    }

    [Fact]
    public void StoredGuildMembershipDurationMinutes_CanBeSet()
    {
        // Arrange
        var options = new GuildMembershipCacheOptions();

        // Act
        options.StoredGuildMembershipDurationMinutes = 60;

        // Assert
        options.StoredGuildMembershipDurationMinutes.Should().Be(60);
    }

    [Fact]
    public void Properties_CanBeSetViaObjectInitializer()
    {
        // Act
        var options = new GuildMembershipCacheOptions
        {
            StoredGuildMembershipDurationMinutes = 45
        };

        // Assert
        options.StoredGuildMembershipDurationMinutes.Should().Be(45);
    }

    [Fact]
    public void DefaultValue_IsReasonableForProduction()
    {
        // Arrange
        var options = new GuildMembershipCacheOptions();

        // Assert
        options.StoredGuildMembershipDurationMinutes.Should().BeGreaterThan(0, "cache duration should be positive");
    }
}
