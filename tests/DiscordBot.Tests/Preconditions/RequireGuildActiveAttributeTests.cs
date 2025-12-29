using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.Tests.Preconditions;

/// <summary>
/// Unit tests for <see cref="RequireGuildActiveAttribute"/>.
/// </summary>
public class RequireGuildActiveAttributeTests
{
    private readonly Mock<IInteractionContext> _mockContext;
    private readonly Mock<ICommandInfo> _mockCommandInfo;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly RequireGuildActiveAttribute _attribute;

    public RequireGuildActiveAttributeTests()
    {
        _mockContext = new Mock<IInteractionContext>();
        _mockCommandInfo = new Mock<ICommandInfo>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockGuildService = new Mock<IGuildService>();
        _attribute = new RequireGuildActiveAttribute();

        // Setup service provider to return the guild service
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IGuildService)))
            .Returns(_mockGuildService.Object);
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenContextGuildIsNull_ShouldReturnSuccess()
    {
        // Arrange - DM context (no guild)
        _mockContext.Setup(c => c.Guild).Returns((IGuild?)null);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue("DM commands should be allowed to pass through");
        result.ErrorReason.Should().BeNull();

        // Verify guild service was never called for DM contexts
        _mockGuildService.Verify(
            gs => gs.GetGuildByIdAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenGuildNotInDatabase_ShouldReturnSuccess()
    {
        // Arrange - Guild context but not in database yet
        var mockGuild = new Mock<IGuild>();
        var guildId = 123456789UL;
        mockGuild.Setup(g => g.Id).Returns(guildId);

        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);

        // Guild service returns null (not in database)
        _mockGuildService
            .Setup(gs => gs.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue("new guilds not yet in database should be allowed");
        result.ErrorReason.Should().BeNull();

        // Verify guild service was called with correct ID
        _mockGuildService.Verify(
            gs => gs.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenGuildIsActive_ShouldReturnSuccess()
    {
        // Arrange - Active guild
        var mockGuild = new Mock<IGuild>();
        var guildId = 123456789UL;
        mockGuild.Setup(g => g.Id).Returns(guildId);

        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);

        var activeGuildDto = new GuildDto
        {
            Id = guildId,
            Name = "Test Guild",
            IsActive = true,
            JoinedAt = DateTime.UtcNow
        };

        _mockGuildService
            .Setup(gs => gs.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeGuildDto);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue("active guilds should be allowed");
        result.ErrorReason.Should().BeNull();

        // Verify guild service was called
        _mockGuildService.Verify(
            gs => gs.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenGuildIsInactive_ShouldReturnError()
    {
        // Arrange - Inactive guild
        var mockGuild = new Mock<IGuild>();
        var guildId = 987654321UL;
        mockGuild.Setup(g => g.Id).Returns(guildId);

        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);

        var inactiveGuildDto = new GuildDto
        {
            Id = guildId,
            Name = "Disabled Guild",
            IsActive = false,
            JoinedAt = DateTime.UtcNow
        };

        _mockGuildService
            .Setup(gs => gs.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveGuildDto);

        // Act
        var result = await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse("inactive guilds should be blocked");
        result.ErrorReason.Should().Be(
            "The bot has been disabled for this server by an administrator.",
            "the error message should inform users the bot is disabled");

        // Verify guild service was called
        _mockGuildService.Verify(
            gs => gs.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckRequirementsAsync_WhenGuildServiceThrowsException_ShouldPropagateException()
    {
        // Arrange - Guild service throws exception
        var mockGuild = new Mock<IGuild>();
        var guildId = 555555555UL;
        mockGuild.Setup(g => g.Id).Returns(guildId);

        _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);

        var expectedException = new InvalidOperationException("Database connection failed");
        _mockGuildService
            .Setup(gs => gs.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var act = async () => await _attribute.CheckRequirementsAsync(
            _mockContext.Object,
            _mockCommandInfo.Object,
            _mockServiceProvider.Object);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database connection failed",
                "exceptions from the guild service should propagate to the caller");

        // Verify guild service was called
        _mockGuildService.Verify(
            gs => gs.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckRequirementsAsync_WithMultipleActiveGuilds_ShouldReturnSuccessForEach()
    {
        // Arrange - Test multiple different active guilds
        var guildIds = new[] { 111111111UL, 222222222UL, 333333333UL };

        foreach (var guildId in guildIds)
        {
            var mockGuild = new Mock<IGuild>();
            mockGuild.Setup(g => g.Id).Returns(guildId);

            _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);

            var activeGuildDto = new GuildDto
            {
                Id = guildId,
                Name = $"Test Guild {guildId}",
                IsActive = true,
                JoinedAt = DateTime.UtcNow
            };

            _mockGuildService
                .Setup(gs => gs.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(activeGuildDto);

            // Act
            var result = await _attribute.CheckRequirementsAsync(
                _mockContext.Object,
                _mockCommandInfo.Object,
                _mockServiceProvider.Object);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeTrue($"guild {guildId} should be allowed");
            result.ErrorReason.Should().BeNull();
        }
    }

    [Fact]
    public async Task CheckRequirementsAsync_WithMultipleInactiveGuilds_ShouldReturnErrorForEach()
    {
        // Arrange - Test multiple different inactive guilds
        var guildIds = new[] { 444444444UL, 555555555UL, 666666666UL };

        foreach (var guildId in guildIds)
        {
            var mockGuild = new Mock<IGuild>();
            mockGuild.Setup(g => g.Id).Returns(guildId);

            _mockContext.Setup(c => c.Guild).Returns(mockGuild.Object);

            var inactiveGuildDto = new GuildDto
            {
                Id = guildId,
                Name = $"Disabled Guild {guildId}",
                IsActive = false,
                JoinedAt = DateTime.UtcNow
            };

            _mockGuildService
                .Setup(gs => gs.GetGuildByIdAsync(guildId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(inactiveGuildDto);

            // Act
            var result = await _attribute.CheckRequirementsAsync(
                _mockContext.Object,
                _mockCommandInfo.Object,
                _mockServiceProvider.Object);

            // Assert
            result.Should().NotBeNull();
            result.IsSuccess.Should().BeFalse($"guild {guildId} should be blocked");
            result.ErrorReason.Should().Be("The bot has been disabled for this server by an administrator.");
        }
    }
}
