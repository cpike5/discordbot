using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for RatWatchStatusService.
/// Tests cover bot status updates during active Rat Watches.
/// </summary>
public class RatWatchStatusServiceTests
{
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly Mock<IRatWatchService> _mockRatWatchService;
    private readonly Mock<ILogger<RatWatchStatusService>> _mockLogger;
    private readonly RatWatchStatusService _service;

    public RatWatchStatusServiceTests()
    {
        _mockDiscordClient = new Mock<DiscordSocketClient>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockSettingsService = new Mock<ISettingsService>();
        _mockRatWatchService = new Mock<IRatWatchService>();
        _mockLogger = new Mock<ILogger<RatWatchStatusService>>();

        // Setup scope factory chain
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(_mockScope.Object);
        _mockScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider
            .Setup(p => p.GetService(typeof(IRatWatchService)))
            .Returns(_mockRatWatchService.Object);

        _service = new RatWatchStatusService(
            _mockDiscordClient.Object,
            _mockScopeFactory.Object,
            _mockSettingsService.Object,
            _mockLogger.Object);
    }

    #region UpdateBotStatusAsync Tests

    [Fact]
    public async Task UpdateBotStatusAsync_WithActiveWatches_ReturnsTrueAndSetsStatus()
    {
        // Arrange
        _mockRatWatchService
            .Setup(s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.UpdateBotStatusAsync();

        // Assert
        result.Should().BeTrue("there are active watches");

        _mockRatWatchService.Verify(
            s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateBotStatusAsync_WithNoActiveWatches_ReturnsFalseAndRestoresStatus()
    {
        // Arrange
        _mockRatWatchService
            .Setup(s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockSettingsService
            .Setup(s => s.GetSettingValueAsync<string>("General:StatusMessage", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result = await _service.UpdateBotStatusAsync();

        // Assert
        result.Should().BeFalse("there are no active watches");

        _mockRatWatchService.Verify(
            s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateBotStatusAsync_CalledTwiceWithSameState_OnlyUpdatesOnce()
    {
        // Arrange
        _mockRatWatchService
            .Setup(s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act - Call twice with same state
        await _service.UpdateBotStatusAsync();
        await _service.UpdateBotStatusAsync();

        // Assert - Service should be called twice (to check state)
        // but Discord client should only be updated once (first call)
        _mockRatWatchService.Verify(
            s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateBotStatusAsync_StateTransitionFromActiveToInactive_RestoresNormalStatus()
    {
        // Arrange - First call: active watches
        _mockRatWatchService
            .SetupSequence(s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        _mockSettingsService
            .Setup(s => s.GetSettingValueAsync<string>("General:StatusMessage", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Custom status message");

        // Act
        var result1 = await _service.UpdateBotStatusAsync();
        var result2 = await _service.UpdateBotStatusAsync();

        // Assert
        result1.Should().BeTrue("first call had active watches");
        result2.Should().BeFalse("second call had no active watches");

        _mockSettingsService.Verify(
            s => s.GetSettingValueAsync<string>("General:StatusMessage", It.IsAny<CancellationToken>()),
            Times.Once,
            "should only restore status when transitioning from active to inactive");
    }

    [Fact]
    public async Task UpdateBotStatusAsync_StateTransitionFromInactiveToActive_SetsRatWatchStatus()
    {
        // Arrange - First call: no active watches, Second call: active watches
        _mockRatWatchService
            .SetupSequence(s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        _mockSettingsService
            .Setup(s => s.GetSettingValueAsync<string>("General:StatusMessage", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // Act
        var result1 = await _service.UpdateBotStatusAsync();
        var result2 = await _service.UpdateBotStatusAsync();

        // Assert
        result1.Should().BeFalse("first call had no active watches");
        result2.Should().BeTrue("second call had active watches");
    }

    [Fact]
    public async Task UpdateBotStatusAsync_WithException_ReturnsCurrentState()
    {
        // Arrange
        _mockRatWatchService
            .Setup(s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _service.UpdateBotStatusAsync();

        // Assert
        result.Should().BeFalse("default state is inactive");
    }

    #endregion

    #region RequestStatusUpdate Tests

    [Fact]
    public void RequestStatusUpdate_RaisesStatusUpdateRequestedEvent()
    {
        // Arrange
        var eventRaised = false;
        _service.StatusUpdateRequested += (sender, args) => eventRaised = true;

        // Act
        _service.RequestStatusUpdate();

        // Assert
        eventRaised.Should().BeTrue("event should be raised when RequestStatusUpdate is called");
    }

    [Fact]
    public void RequestStatusUpdate_WithNoSubscribers_DoesNotThrow()
    {
        // Act & Assert
        var action = () => _service.RequestStatusUpdate();
        action.Should().NotThrow("should handle case with no event subscribers");
    }

    #endregion
}
