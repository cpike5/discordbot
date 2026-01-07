using DiscordBot.Bot.Hubs;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Unit tests for <see cref="PerformanceNotifier"/>.
/// Tests SignalR broadcast operations for performance alerts.
/// </summary>
public class PerformanceNotifierTests
{
    private readonly Mock<IHubContext<DashboardHub>> _mockHubContext;
    private readonly Mock<IHubClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IServiceScope> _mockServiceScope;
    private readonly Mock<IServiceProvider> _mockScopedServiceProvider;
    private readonly Mock<IPerformanceAlertRepository> _mockRepository;
    private readonly Mock<ILogger<PerformanceNotifier>> _mockLogger;
    private readonly PerformanceNotifier _notifier;

    public PerformanceNotifierTests()
    {
        _mockHubContext = new Mock<IHubContext<DashboardHub>>();
        _mockClients = new Mock<IHubClients>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockServiceScope = new Mock<IServiceScope>();
        _mockScopedServiceProvider = new Mock<IServiceProvider>();
        _mockRepository = new Mock<IPerformanceAlertRepository>();
        _mockLogger = new Mock<ILogger<PerformanceNotifier>>();

        // Setup hub context with clients
        _mockHubContext
            .Setup(h => h.Clients)
            .Returns(_mockClients.Object);

        // Setup clients to return group proxy
        _mockClients
            .Setup(c => c.Group(DashboardHub.AlertsGroupName))
            .Returns(_mockClientProxy.Object);

        // Setup scope's service provider to return repository when GetRequiredService is called
        _mockServiceScope
            .Setup(s => s.ServiceProvider)
            .Returns(_mockScopedServiceProvider.Object);

        _mockScopedServiceProvider
            .Setup(sp => sp.GetService(typeof(IPerformanceAlertRepository)))
            .Returns(_mockRepository.Object);

        // Mock the IServiceProvider.GetService method to return the service scope factory
        _mockServiceProvider
            .Setup(sp => sp.GetService(typeof(IServiceScopeFactory)))
            .Returns(CreateMockServiceScopeFactory());

        // Configure client proxy default behavior - return completed tasks for all calls
        // Note: We can't mock the SendAsync extension method directly with Moq,
        // but the underlying method will return a completed task by default
        _mockClientProxy.DefaultValue = DefaultValue.Mock;

        _notifier = new PerformanceNotifier(
            _mockHubContext.Object,
            _mockServiceProvider.Object,
            _mockLogger.Object);
    }

    /// <summary>
    /// Helper method to create a mock service scope factory that returns our mock scope.
    /// </summary>
    private IServiceScopeFactory CreateMockServiceScopeFactory()
    {
        var mockFactory = new Mock<IServiceScopeFactory>();
        mockFactory
            .Setup(f => f.CreateScope())
            .Returns(_mockServiceScope.Object);
        return mockFactory.Object;
    }

    #region BroadcastAlertTriggeredAsync Tests

    [Fact]
    public async Task BroadcastAlertTriggeredAsync_ShouldNotThrow()
    {
        // Arrange
        var incident = new PerformanceIncidentDto
        {
            Id = Guid.NewGuid(),
            MetricName = "CommandResponseTime",
            Severity = AlertSeverity.Warning,
            Status = IncidentStatus.Active,
            TriggeredAt = DateTime.UtcNow,
            ThresholdValue = 1000,
            ActualValue = 1500,
            Message = "Command response time exceeded threshold"
        };

        _mockRepository
            .Setup(r => r.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PerformanceIncident>().AsReadOnly());

        // Act & Assert
        var act = () => _notifier.BroadcastAlertTriggeredAsync(incident);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BroadcastAlertTriggeredAsync_ShouldLogDebugMessage()
    {
        // Arrange
        var incident = new PerformanceIncidentDto
        {
            Id = Guid.NewGuid(),
            MetricName = "MemoryUsage",
            Severity = AlertSeverity.Warning,
            Status = IncidentStatus.Active,
            TriggeredAt = DateTime.UtcNow,
            ThresholdValue = 512,
            ActualValue = 768,
            Message = "Memory usage exceeded warning threshold"
        };

        _mockRepository
            .Setup(r => r.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PerformanceIncident>().AsReadOnly());

        // Act
        await _notifier.BroadcastAlertTriggeredAsync(incident);

        // Assert - verify debug log was called
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Broadcasting alert triggered event")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when broadcasting alert triggered");
    }

    [Fact]
    public async Task BroadcastAlertTriggeredAsync_ShouldLogTraceMessage()
    {
        // Arrange
        var incident = new PerformanceIncidentDto
        {
            Id = Guid.NewGuid(),
            MetricName = "DatabaseQueryTime",
            Severity = AlertSeverity.Critical,
            Status = IncidentStatus.Active,
            TriggeredAt = DateTime.UtcNow,
            ThresholdValue = 500,
            ActualValue = 800,
            Message = "Database query time exceeded threshold"
        };

        _mockRepository
            .Setup(r => r.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PerformanceIncident>().AsReadOnly());

        // Act
        await _notifier.BroadcastAlertTriggeredAsync(incident);

        // Assert - verify trace log was called
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Alert triggered event broadcast completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log trace message when broadcast completes");
    }


    #endregion

    #region BroadcastAlertResolvedAsync Tests

    [Fact]
    public async Task BroadcastAlertResolvedAsync_ShouldNotThrow()
    {
        // Arrange
        var incident = new PerformanceIncidentDto
        {
            Id = Guid.NewGuid(),
            MetricName = "CommandResponseTime",
            Severity = AlertSeverity.Warning,
            Status = IncidentStatus.Resolved,
            TriggeredAt = DateTime.UtcNow.AddMinutes(-10),
            ResolvedAt = DateTime.UtcNow,
            ThresholdValue = 1000,
            ActualValue = 500,
            Message = "Command response time returned to normal"
        };

        _mockRepository
            .Setup(r => r.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PerformanceIncident>().AsReadOnly());

        // Act & Assert
        var act = () => _notifier.BroadcastAlertResolvedAsync(incident);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BroadcastAlertResolvedAsync_ShouldLogDebugMessage()
    {
        // Arrange
        var incident = new PerformanceIncidentDto
        {
            Id = Guid.NewGuid(),
            MetricName = "MemoryUsage",
            Severity = AlertSeverity.Warning,
            Status = IncidentStatus.Resolved,
            TriggeredAt = DateTime.UtcNow.AddMinutes(-5),
            ResolvedAt = DateTime.UtcNow,
            ThresholdValue = 512,
            ActualValue = 300,
            Message = "Memory usage resolved"
        };

        _mockRepository
            .Setup(r => r.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PerformanceIncident>().AsReadOnly());

        // Act
        await _notifier.BroadcastAlertResolvedAsync(incident);

        // Assert - verify debug log
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Broadcasting alert resolved event")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when broadcasting alert resolved");
    }

    [Fact]
    public async Task BroadcastAlertResolvedAsync_ShouldLogTraceMessage()
    {
        // Arrange
        var incident = new PerformanceIncidentDto
        {
            Id = Guid.NewGuid(),
            MetricName = "DatabaseQueryTime",
            Severity = AlertSeverity.Critical,
            Status = IncidentStatus.Resolved,
            TriggeredAt = DateTime.UtcNow.AddMinutes(-15),
            ResolvedAt = DateTime.UtcNow,
            ThresholdValue = 500,
            ActualValue = 200,
            Message = "Database query time resolved"
        };

        _mockRepository
            .Setup(r => r.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PerformanceIncident>().AsReadOnly());

        // Act
        await _notifier.BroadcastAlertResolvedAsync(incident);

        // Assert - verify trace log
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Alert resolved event broadcast completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log trace message when broadcast completes");
    }


    #endregion

    #region BroadcastAlertAcknowledgedAsync Tests

    [Fact]
    public async Task BroadcastAlertAcknowledgedAsync_ShouldNotThrow()
    {
        // Arrange
        var incidentId = Guid.NewGuid();
        const string acknowledgedBy = "admin-user-123";

        // Act & Assert
        var act = () => _notifier.BroadcastAlertAcknowledgedAsync(incidentId, acknowledgedBy);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BroadcastAlertAcknowledgedAsync_ShouldLogDebugMessage()
    {
        // Arrange
        var incidentId = Guid.NewGuid();
        const string acknowledgedBy = "admin-user-123";

        // Act
        await _notifier.BroadcastAlertAcknowledgedAsync(incidentId, acknowledgedBy);

        // Assert - verify debug log
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Broadcasting alert acknowledged event")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log debug message when broadcasting alert acknowledged");
    }

    [Fact]
    public async Task BroadcastAlertAcknowledgedAsync_ShouldLogTraceMessage()
    {
        // Arrange
        var incidentId = Guid.NewGuid();
        const string acknowledgedBy = "admin-user-123";

        // Act
        await _notifier.BroadcastAlertAcknowledgedAsync(incidentId, acknowledgedBy);

        // Assert - verify trace log
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Alert acknowledged event broadcast completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log trace message when broadcast completes");
    }


    #endregion

    #region BroadcastActiveAlertCountAsync Tests

    [Fact]
    public async Task BroadcastActiveAlertCountAsync_ShouldGetActiveIncidentsFromRepository()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PerformanceIncident>().AsReadOnly());

        // Act
        await _notifier.BroadcastActiveAlertCountAsync();

        // Assert
        _mockRepository.Verify(
            r => r.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()),
            Times.Once,
            "Should retrieve active incidents from repository");
    }

    [Fact]
    public async Task BroadcastActiveAlertCountAsync_WithCriticalIncidents_ShouldRetrieveAllIncidents()
    {
        // Arrange
        var incidents = new List<PerformanceIncident>
        {
            new() { Id = Guid.NewGuid(), Severity = AlertSeverity.Critical, Status = IncidentStatus.Active },
            new() { Id = Guid.NewGuid(), Severity = AlertSeverity.Critical, Status = IncidentStatus.Active },
            new() { Id = Guid.NewGuid(), Severity = AlertSeverity.Warning, Status = IncidentStatus.Active },
            new() { Id = Guid.NewGuid(), Severity = AlertSeverity.Info, Status = IncidentStatus.Active }
        };

        _mockRepository
            .Setup(r => r.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(incidents.AsReadOnly());

        // Act
        await _notifier.BroadcastActiveAlertCountAsync();

        // Assert
        _mockRepository.Verify(
            r => r.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()),
            Times.Once,
            "Should retrieve all active incidents");
    }

    [Fact]
    public async Task BroadcastActiveAlertCountAsync_ShouldLogTraceMessage()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PerformanceIncident>().AsReadOnly());

        // Act
        await _notifier.BroadcastActiveAlertCountAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Active alert count broadcast completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log trace message when broadcast completes");
    }

    [Fact]
    public async Task BroadcastActiveAlertCountAsync_WhenRepositoryThrows_ShouldLogWarningAndNotThrow()
    {
        // Arrange
        var repositoryException = new InvalidOperationException("Database connection failed");

        _mockRepository
            .Setup(r => r.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(repositoryException);

        // Act & Assert - should not throw
        var act = () => _notifier.BroadcastActiveAlertCountAsync();
        await act.Should().NotThrowAsync();

        // Verify warning was logged
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Error broadcasting active alert count")),
                repositoryException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log warning when repository fails");
    }


    #endregion

    #region Integration Tests

    [Fact]
    public async Task BroadcastAlertTriggeredAsync_FollowedByResolved_ShouldBroadcastBothWithoutThrow()
    {
        // Arrange
        var incidentId = Guid.NewGuid();
        var triggeredIncident = new PerformanceIncidentDto
        {
            Id = incidentId,
            MetricName = "TestMetric",
            Severity = AlertSeverity.Warning,
            Status = IncidentStatus.Active,
            TriggeredAt = DateTime.UtcNow,
            ThresholdValue = 100,
            ActualValue = 150,
            Message = "Alert triggered"
        };

        var resolvedIncident = new PerformanceIncidentDto
        {
            Id = incidentId,
            MetricName = "TestMetric",
            Severity = AlertSeverity.Warning,
            Status = IncidentStatus.Resolved,
            TriggeredAt = DateTime.UtcNow.AddMinutes(-5),
            ResolvedAt = DateTime.UtcNow,
            ThresholdValue = 100,
            ActualValue = 50,
            Message = "Alert resolved"
        };

        _mockRepository
            .Setup(r => r.GetActiveIncidentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PerformanceIncident>().AsReadOnly());

        // Act & Assert
        var act1 = () => _notifier.BroadcastAlertTriggeredAsync(triggeredIncident);
        await act1.Should().NotThrowAsync("Should broadcast triggered without throwing");

        var act2 = () => _notifier.BroadcastAlertResolvedAsync(resolvedIncident);
        await act2.Should().NotThrowAsync("Should broadcast resolved without throwing");
    }

    #endregion
}
