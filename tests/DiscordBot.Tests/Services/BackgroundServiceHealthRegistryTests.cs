using DiscordBot.Bot.Services;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="BackgroundServiceHealthRegistry"/>.
/// Tests cover service registration, unregistration, health status retrieval,
/// and overall health status aggregation logic.
/// </summary>
public class BackgroundServiceHealthRegistryTests
{
    private readonly BackgroundServiceHealthRegistry _registry;

    public BackgroundServiceHealthRegistryTests()
    {
        _registry = new BackgroundServiceHealthRegistry(
            NullLogger<BackgroundServiceHealthRegistry>.Instance);
    }

    [Fact]
    public void Register_AddsServiceToRegistry()
    {
        // Arrange
        var mockService = CreateMockService("TestService", "Running", DateTime.UtcNow, null);

        // Act
        _registry.Register("TestService", mockService.Object);

        // Assert
        var allHealth = _registry.GetAllHealth();
        allHealth.Should().HaveCount(1, "one service was registered");
        allHealth[0].ServiceName.Should().Be("TestService", "service name should match");
    }

    [Fact]
    public void Unregister_RemovesServiceFromRegistry()
    {
        // Arrange
        var mockService = CreateMockService("TestService", "Running", DateTime.UtcNow, null);
        _registry.Register("TestService", mockService.Object);

        // Act
        _registry.Unregister("TestService");

        // Assert
        var allHealth = _registry.GetAllHealth();
        allHealth.Should().BeEmpty("service should be removed from registry");
    }

    [Fact]
    public void GetAllHealth_ReturnsAllRegisteredServices()
    {
        // Arrange
        var service1 = CreateMockService("Service1", "Running", DateTime.UtcNow, null);
        var service2 = CreateMockService("Service2", "Running", DateTime.UtcNow, null);
        var service3 = CreateMockService("Service3", "Stopped", DateTime.UtcNow, "Stopped by user");

        _registry.Register("Service1", service1.Object);
        _registry.Register("Service2", service2.Object);
        _registry.Register("Service3", service3.Object);

        // Act
        var allHealth = _registry.GetAllHealth();

        // Assert
        allHealth.Should().HaveCount(3, "three services were registered");
        allHealth.Should().Contain(h => h.ServiceName == "Service1", "Service1 should be included");
        allHealth.Should().Contain(h => h.ServiceName == "Service2", "Service2 should be included");
        allHealth.Should().Contain(h => h.ServiceName == "Service3", "Service3 should be included");
    }

    [Fact]
    public void GetHealth_ReturnsNullForUnknownService()
    {
        // Act
        var health = _registry.GetHealth("NonExistentService");

        // Assert
        health.Should().BeNull("service does not exist in registry");
    }

    [Fact]
    public void GetOverallStatus_ReturnsHealthyWhenAllHealthy()
    {
        // Arrange - All services running with recent heartbeats
        var service1 = CreateMockService("Service1", "Running", DateTime.UtcNow, null);
        var service2 = CreateMockService("Service2", "Running", DateTime.UtcNow, null);

        _registry.Register("Service1", service1.Object);
        _registry.Register("Service2", service2.Object);

        // Act
        var overallStatus = _registry.GetOverallStatus();

        // Assert
        overallStatus.Should().Be("Healthy", "all services are running normally");
    }

    [Fact]
    public void GetOverallStatus_ReturnsDegradedWhenAnyDegraded()
    {
        // Arrange - One service stopped, others running
        var service1 = CreateMockService("Service1", "Running", DateTime.UtcNow, null);
        var service2 = CreateMockService("Service2", "Stopped", DateTime.UtcNow, null);

        _registry.Register("Service1", service1.Object);
        _registry.Register("Service2", service2.Object);

        // Act
        var overallStatus = _registry.GetOverallStatus();

        // Assert
        overallStatus.Should().Be("Degraded", "one service is stopped");
    }

    [Fact]
    public void GetOverallStatus_ReturnsUnhealthyWhenAnyError()
    {
        // Arrange - One service with error, others running
        var service1 = CreateMockService("Service1", "Running", DateTime.UtcNow, null);
        var service2 = CreateMockService("Service2", "Error", DateTime.UtcNow, "Fatal exception");

        _registry.Register("Service1", service1.Object);
        _registry.Register("Service2", service2.Object);

        // Act
        var overallStatus = _registry.GetOverallStatus();

        // Assert
        overallStatus.Should().Be("Unhealthy", "one service has an error");
    }

    /// <summary>
    /// Creates a mock IBackgroundServiceHealth with specified properties.
    /// </summary>
    private static Mock<IBackgroundServiceHealth> CreateMockService(
        string serviceName,
        string status,
        DateTime? lastHeartbeat,
        string? lastError)
    {
        var mock = new Mock<IBackgroundServiceHealth>();
        mock.Setup(s => s.ServiceName).Returns(serviceName);
        mock.Setup(s => s.Status).Returns(status);
        mock.Setup(s => s.LastHeartbeat).Returns(lastHeartbeat);
        mock.Setup(s => s.LastError).Returns(lastError);
        return mock;
    }
}
