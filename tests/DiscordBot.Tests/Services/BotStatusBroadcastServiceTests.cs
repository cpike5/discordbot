using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="BotStatusBroadcastService"/>.
/// Tests call the internal <see cref="BotStatusBroadcastService.BroadcastBotStatusAsync"/>
/// directly rather than driving the real 30-second <see cref="PeriodicTimer"/> loop, matching
/// the pattern used by <see cref="PerformanceMetricsBroadcastServiceTests"/>.
/// </summary>
public class BotStatusBroadcastServiceTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IBotStatusBroadcaster> _mockBotStatusBroadcaster;
    private readonly Mock<ILogger<BotStatusBroadcastService>> _mockLogger;
    private readonly BotStatusBroadcastService _service;

    public BotStatusBroadcastServiceTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockBotStatusBroadcaster = new Mock<IBotStatusBroadcaster>();
        _mockLogger = new Mock<ILogger<BotStatusBroadcastService>>();

        _service = new BotStatusBroadcastService(
            _mockServiceProvider.Object,
            _mockBotStatusBroadcaster.Object,
            _mockLogger.Object);
    }

    [Fact]
    public void ServiceName_ShouldReturnExpectedName()
    {
        _service.ServiceName.Should().Be("Bot Status Broadcast Service");
    }

    [Fact]
    public async Task BroadcastBotStatusAsync_ShouldDelegateToBotStatusBroadcaster()
    {
        // Act
        await _service.BroadcastBotStatusAsync();

        // Assert - reuses IBotStatusBroadcaster.BroadcastStatusAsync (the same method the
        // connect/disconnect path uses) so the payload is built and sent identically.
        _mockBotStatusBroadcaster.Verify(x => x.BroadcastStatusAsync(), Times.Once);
    }

    [Fact]
    public async Task BroadcastBotStatusAsync_CalledMultipleTimes_ShouldDelegateEachTime()
    {
        // Act
        await _service.BroadcastBotStatusAsync();
        await _service.BroadcastBotStatusAsync();
        await _service.BroadcastBotStatusAsync();

        // Assert
        _mockBotStatusBroadcaster.Verify(x => x.BroadcastStatusAsync(), Times.Exactly(3));
    }

    [Fact]
    public async Task BroadcastBotStatusAsync_WhenBroadcasterThrows_ShouldPropagate()
    {
        // Arrange - the ExecuteMonitoredAsync loop is responsible for catching and recording
        // errors (see RecordError/ClearError in the timer loop); the delegating method itself
        // should not swallow exceptions.
        _mockBotStatusBroadcaster
            .Setup(x => x.BroadcastStatusAsync())
            .ThrowsAsync(new InvalidOperationException("boom"));

        // Act
        var act = () => _service.BroadcastBotStatusAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
