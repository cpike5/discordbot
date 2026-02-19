using DiscordBot.Bot.Health;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscordBot.Tests.Health;

/// <summary>
/// Unit tests for <see cref="DatabaseHealthCheck"/>.
/// Tests the happy path (reachable database) using the in-memory SQLite context from
/// <see cref="TestDbContextFactory"/>, and the failure path using a faulting scope factory.
/// Timeout behavior is not tested here as it is inherently time-dependent and flaky in CI.
/// </summary>
public class DatabaseHealthCheckTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly DatabaseHealthCheck _healthCheck;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;

    public DatabaseHealthCheckTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();

        // Wire up a scope factory that resolves the real in-memory BotDbContext
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(BotDbContext)))
            .Returns(_context);

        var serviceScopeMock = new Mock<IServiceScope>();
        serviceScopeMock
            .Setup(s => s.ServiceProvider)
            .Returns(serviceProviderMock.Object);

        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeFactoryMock
            .Setup(f => f.CreateScope())
            .Returns(serviceScopeMock.Object);

        _healthCheck = new DatabaseHealthCheck(
            _scopeFactoryMock.Object,
            NullLogger<DatabaseHealthCheck>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseIsReachable_ReturnsHealthy()
    {
        // Act
        var result = await _healthCheck.CheckHealthAsync(null!, CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy,
            "a reachable database should produce a Healthy result");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseIsReachable_ReturnsHealthyDescription()
    {
        // Act
        var result = await _healthCheck.CheckHealthAsync(null!, CancellationToken.None);

        // Assert
        result.Description.Should().NotBeNullOrWhiteSpace(
            "a Healthy result should include a description");
        result.Description.Should().ContainEquivalentOf("healthy",
            "the description should indicate the database is healthy");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseThrowsException_ReturnsUnhealthy()
    {
        // Arrange — configure the scope factory to throw when the scope is created
        var throwingScopeFactoryMock = new Mock<IServiceScopeFactory>();
        throwingScopeFactoryMock
            .Setup(f => f.CreateScope())
            .Throws(new InvalidOperationException("Simulated database failure"));

        var failingCheck = new DatabaseHealthCheck(
            throwingScopeFactoryMock.Object,
            NullLogger<DatabaseHealthCheck>.Instance);

        // Act
        var result = await failingCheck.CheckHealthAsync(null!, CancellationToken.None);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy,
            "an exception during database access should produce an Unhealthy result");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseThrowsException_ReturnsUnhealthyDescription()
    {
        // Arrange
        var throwingScopeFactoryMock = new Mock<IServiceScopeFactory>();
        throwingScopeFactoryMock
            .Setup(f => f.CreateScope())
            .Throws(new InvalidOperationException("Simulated database failure"));

        var failingCheck = new DatabaseHealthCheck(
            throwingScopeFactoryMock.Object,
            NullLogger<DatabaseHealthCheck>.Instance);

        // Act
        var result = await failingCheck.CheckHealthAsync(null!, CancellationToken.None);

        // Assert
        result.Description.Should().NotBeNullOrWhiteSpace(
            "an Unhealthy result should include a description");
        result.Description.Should().ContainEquivalentOf("failed",
            "the description should indicate the connection failed");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenDatabaseIsReachable_CreatesExactlyOneScope()
    {
        // Act
        await _healthCheck.CheckHealthAsync(null!, CancellationToken.None);

        // Assert
        _scopeFactoryMock.Verify(
            f => f.CreateScope(),
            Times.Once,
            "the health check should create exactly one scope per invocation");
    }
}
