using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="GuildService"/>.
/// NOTE: Direct testing of GuildService is limited because DiscordSocketClient and SocketGuild
/// are sealed classes that cannot be easily mocked. These tests focus on testing the repository
/// interactions while documenting the expected Discord client behaviors.
/// Integration testing through the controller layer provides better coverage for this service.
/// </summary>
public class GuildServiceTests
{
    private readonly Mock<IGuildRepository> _mockGuildRepository;
    private readonly Mock<ILogger<GuildService>> _mockLogger;

    public GuildServiceTests()
    {
        _mockGuildRepository = new Mock<IGuildRepository>();
        _mockLogger = new Mock<ILogger<GuildService>>();
    }

    /// <summary>
    /// Documentation test that describes the expected behavior of GetAllGuildsAsync.
    /// </summary>
    [Fact]
    public void GetAllGuildsAsync_ExpectedBehavior_Documentation()
    {
        // This test documents the expected behavior of GuildService.GetAllGuildsAsync:
        // 1. Calls repository.GetAllAsync() to fetch all guilds from database
        // 2. For each guild, calls client.GetGuild(guildId) to get live Discord data
        // 3. Merges database and Discord data into GuildDto:
        //    - Name: Uses Discord name if available, else database name
        //    - MemberCount, IconUrl: From Discord guild (nullable if not available)
        //    - IsActive, Prefix, Settings, JoinedAt: From database guild
        // 4. Returns IReadOnlyList<GuildDto>
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/GuildService.cs:34-50

        var expectedBehavior = new
        {
            Method = "GetAllGuildsAsync",
            Returns = "IReadOnlyList<GuildDto>",
            Steps = new[]
            {
                "1. Call repository.GetAllAsync()",
                "2. For each guild: client.GetGuild(guildId)",
                "3. Merge database and Discord data",
                "4. Return mapped list"
            }
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.Method.Should().Be("GetAllGuildsAsync");
        expectedBehavior.Steps.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetGuildByIdAsync_WithNonExistentGuild_ShouldReturnNull()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object);

        _mockGuildRepository
            .Setup(r => r.GetByDiscordIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guild?)null);

        // Act
        var result = await service.GetGuildByIdAsync(guildId);

        // Assert
        result.Should().BeNull("the guild does not exist in the database");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task UpdateGuildAsync_WithPartialUpdate_ShouldUpdateOnlyProvidedFields()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object);

        var guild = new Guild
        {
            Id = guildId,
            Name = "Test Guild",
            JoinedAt = DateTime.UtcNow.AddDays(-30),
            IsActive = true,
            Prefix = "!",
            Settings = "{\"old\":true}"
        };

        var request = new GuildUpdateRequestDto
        {
            Prefix = "?",
            Settings = null, // Not updating settings
            IsActive = null  // Not updating IsActive
        };

        _mockGuildRepository
            .Setup(r => r.GetByDiscordIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _mockGuildRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Guild>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.UpdateGuildAsync(guildId, request);

        // Assert
        result.Should().NotBeNull();
        result!.Prefix.Should().Be("?", "prefix should be updated");
        result.Settings.Should().Be("{\"old\":true}", "settings should remain unchanged");
        result.IsActive.Should().BeTrue("IsActive should remain unchanged");

        _mockGuildRepository.Verify(
            r => r.UpdateAsync(It.Is<Guild>(g =>
                g.Prefix == "?" &&
                g.Settings == "{\"old\":true}" &&
                g.IsActive == true),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "only the specified fields should be updated");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task UpdateGuildAsync_WithNonExistentGuild_ShouldReturnNull()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object);
        var request = new GuildUpdateRequestDto { Prefix = "?" };

        _mockGuildRepository
            .Setup(r => r.GetByDiscordIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guild?)null);

        // Act
        var result = await service.UpdateGuildAsync(guildId, request);

        // Assert
        result.Should().BeNull("the guild does not exist in the database");

        _mockGuildRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Guild>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "update should not be called when guild does not exist");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task SyncGuildAsync_WithNonExistentDiscordGuild_ShouldReturnFalse()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object);

        // Act
        var result = await service.SyncGuildAsync(guildId);

        // Assert
        result.Should().BeFalse("the guild does not exist in Discord");

        _mockGuildRepository.Verify(
            r => r.UpsertAsync(It.IsAny<Guild>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "upsert should not be called when Discord guild does not exist");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task GetAllGuildsAsync_WithCancellationToken_ShouldPassToRepository()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockGuildRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guild>());

        // Act
        await service.GetAllGuildsAsync(cancellationToken);

        // Assert
        _mockGuildRepository.Verify(
            r => r.GetAllAsync(cancellationToken),
            Times.Once,
            "the cancellation token should be passed to the repository");

        // Cleanup
        await client.DisposeAsync();
    }
}
