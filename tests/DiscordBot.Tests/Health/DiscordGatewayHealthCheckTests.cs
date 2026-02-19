using DiscordBot.Bot.Health;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;

namespace DiscordBot.Tests.Health;

/// <summary>
/// Unit tests for <see cref="DiscordGatewayHealthCheck"/>.
/// Covers the mapping from each <see cref="GatewayConnectionState"/> value
/// to the expected <see cref="HealthStatus"/>.
/// </summary>
public class DiscordGatewayHealthCheckTests
{
    private readonly Mock<IConnectionStateService> _connectionStateServiceMock;
    private readonly DiscordGatewayHealthCheck _healthCheck;

    public DiscordGatewayHealthCheckTests()
    {
        _connectionStateServiceMock = new Mock<IConnectionStateService>();
        _healthCheck = new DiscordGatewayHealthCheck(_connectionStateServiceMock.Object);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnected_ReturnsHealthy()
    {
        // Arrange
        _connectionStateServiceMock
            .Setup(s => s.GetCurrentState())
            .Returns(GatewayConnectionState.Connected);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!, CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy,
            "a Connected gateway state should map to Healthy");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnected_ReturnsDescriptionContainingConnected()
    {
        // Arrange
        _connectionStateServiceMock
            .Setup(s => s.GetCurrentState())
            .Returns(GatewayConnectionState.Connected);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!, CancellationToken.None);

        // Assert
        result.Description.Should().ContainEquivalentOf("connected",
            "the description should mention the connected state");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnecting_ReturnsDegraded()
    {
        // Arrange
        _connectionStateServiceMock
            .Setup(s => s.GetCurrentState())
            .Returns(GatewayConnectionState.Connecting);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!, CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded,
            "a Connecting gateway state should map to Degraded while the connection is in progress");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnecting_ReturnsDescriptionContainingConnecting()
    {
        // Arrange
        _connectionStateServiceMock
            .Setup(s => s.GetCurrentState())
            .Returns(GatewayConnectionState.Connecting);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!, CancellationToken.None);

        // Assert
        result.Description.Should().ContainEquivalentOf("connecting",
            "the description should mention the connecting state");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDisconnected_ReturnsDegraded()
    {
        // Arrange
        _connectionStateServiceMock
            .Setup(s => s.GetCurrentState())
            .Returns(GatewayConnectionState.Disconnected);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!, CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded,
            "a Disconnected gateway state should map to Degraded");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDisconnected_ReturnsDescriptionContainingDisconnected()
    {
        // Arrange
        _connectionStateServiceMock
            .Setup(s => s.GetCurrentState())
            .Returns(GatewayConnectionState.Disconnected);

        // Act
        var result = await _healthCheck.CheckHealthAsync(null!, CancellationToken.None);

        // Assert
        result.Description.Should().ContainEquivalentOf("disconnected",
            "the description should mention the disconnected state");
    }

    [Fact]
    public async Task CheckHealthAsync_CallsGetCurrentStateExactlyOnce()
    {
        // Arrange
        _connectionStateServiceMock
            .Setup(s => s.GetCurrentState())
            .Returns(GatewayConnectionState.Connected);

        // Act
        await _healthCheck.CheckHealthAsync(null!, CancellationToken.None);

        // Assert
        _connectionStateServiceMock.Verify(
            s => s.GetCurrentState(),
            Times.Once,
            "the health check should query the connection state exactly once per invocation");
    }
}
