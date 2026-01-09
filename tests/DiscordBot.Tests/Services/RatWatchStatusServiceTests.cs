using Discord.WebSocket;
using DiscordBot.Bot.Services.RatWatch;
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
    private readonly Mock<IBotStatusService> _mockBotStatusService;
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory;
    private readonly Mock<IServiceScope> _mockScope;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IRatWatchService> _mockRatWatchService;
    private readonly Mock<ILogger<RatWatchStatusService>> _mockLogger;
    private readonly RatWatchStatusService _service;

    public RatWatchStatusServiceTests()
    {
        _mockBotStatusService = new Mock<IBotStatusService>();
        _mockScopeFactory = new Mock<IServiceScopeFactory>();
        _mockScope = new Mock<IServiceScope>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockRatWatchService = new Mock<IRatWatchService>();
        _mockLogger = new Mock<ILogger<RatWatchStatusService>>();

        // Setup scope factory chain
        _mockScopeFactory.Setup(f => f.CreateScope()).Returns(_mockScope.Object);
        _mockScope.Setup(s => s.ServiceProvider).Returns(_mockServiceProvider.Object);
        _mockServiceProvider
            .Setup(p => p.GetService(typeof(IRatWatchService)))
            .Returns(_mockRatWatchService.Object);

        _service = new RatWatchStatusService(
            _mockBotStatusService.Object,
            _mockScopeFactory.Object,
            _mockLogger.Object);
    }

    #region UpdateBotStatusAsync Tests

    [Fact]
    public async Task UpdateBotStatusAsync_WithActiveWatches_ReturnsTrueAndRefreshesStatus()
    {
        // Arrange
        _mockRatWatchService
            .Setup(s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.UpdateBotStatusAsync();

        // Assert
        result.Should().BeTrue("there are active watches");

        _mockBotStatusService.Verify(
            s => s.RefreshStatusAsync(),
            Times.Once,
            "should trigger status refresh");

        _mockRatWatchService.Verify(
            s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateBotStatusAsync_WithNoActiveWatches_ReturnsFalseAndRefreshesStatus()
    {
        // Arrange
        _mockRatWatchService
            .Setup(s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.UpdateBotStatusAsync();

        // Assert
        result.Should().BeFalse("there are no active watches");

        _mockBotStatusService.Verify(
            s => s.RefreshStatusAsync(),
            Times.Once,
            "should trigger status refresh");

        _mockRatWatchService.Verify(
            s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateBotStatusAsync_CalledTwiceWithSameState_RefreshesStatusBothTimes()
    {
        // Arrange
        _mockRatWatchService
            .Setup(s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act - Call twice with same state
        await _service.UpdateBotStatusAsync();
        await _service.UpdateBotStatusAsync();

        // Assert - Status refresh is delegated to BotStatusService, which handles state caching
        _mockBotStatusService.Verify(
            s => s.RefreshStatusAsync(),
            Times.Exactly(2),
            "should refresh status each time");

        _mockRatWatchService.Verify(
            s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task UpdateBotStatusAsync_StateTransitionFromActiveToInactive_TriggersRefresh()
    {
        // Arrange - First call: active watches, Second call: no active watches
        _mockRatWatchService
            .SetupSequence(s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);

        // Act
        var result1 = await _service.UpdateBotStatusAsync();
        var result2 = await _service.UpdateBotStatusAsync();

        // Assert
        result1.Should().BeTrue("first call had active watches");
        result2.Should().BeFalse("second call had no active watches");

        _mockBotStatusService.Verify(
            s => s.RefreshStatusAsync(),
            Times.Exactly(2),
            "should refresh status on both calls");
    }

    [Fact]
    public async Task UpdateBotStatusAsync_StateTransitionFromInactiveToActive_TriggersRefresh()
    {
        // Arrange - First call: no active watches, Second call: active watches
        _mockRatWatchService
            .SetupSequence(s => s.HasActiveWatchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        // Act
        var result1 = await _service.UpdateBotStatusAsync();
        var result2 = await _service.UpdateBotStatusAsync();

        // Assert
        result1.Should().BeFalse("first call had no active watches");
        result2.Should().BeTrue("second call had active watches");

        _mockBotStatusService.Verify(
            s => s.RefreshStatusAsync(),
            Times.Exactly(2),
            "should refresh status on both calls");
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
