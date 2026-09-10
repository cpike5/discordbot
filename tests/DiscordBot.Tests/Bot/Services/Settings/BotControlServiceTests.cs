using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Settings;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services.Settings;

/// <summary>
/// Unit tests for <see cref="BotControlService"/>.
/// </summary>
public class BotControlServiceTests
{
    private readonly Mock<IBotService> _mockBotService = new();
    private readonly Mock<IAuditLogQueue> _mockAuditLogQueue = new();
    private readonly BotControlService _service;

    public BotControlServiceTests()
    {
        _service = new BotControlService(
            _mockBotService.Object,
            _mockAuditLogQueue.Object,
            Mock.Of<ILogger<BotControlService>>());
    }

    [Fact]
    public async Task RestartAsync_WhenRestartSucceeds_ReturnsSuccessAndAuditLogs()
    {
        // Arrange
        _mockBotService.Setup(b => b.RestartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await _service.RestartAsync("user-1");

        // Assert
        result.Success.Should().BeTrue();
        _mockAuditLogQueue.Verify(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()), Times.Once);
    }

    [Fact]
    public async Task RestartAsync_WhenBotServiceThrows_ReturnsFailureWithoutAuditLog()
    {
        // Arrange
        _mockBotService.Setup(b => b.RestartAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var result = await _service.RestartAsync("user-1");

        // Assert
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(500);
        _mockAuditLogQueue.Verify(q => q.Enqueue(It.IsAny<AuditLogCreateDto>()), Times.Never);
    }
}
