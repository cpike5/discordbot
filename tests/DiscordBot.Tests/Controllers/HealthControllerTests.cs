using DiscordBot.Bot.Controllers;
using DiscordBot.Core.DTOs;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="HealthController"/>.
/// </summary>
public class HealthControllerTests : IDisposable
{
    private readonly BotDbContext _dbContext;
    private readonly SqliteConnection _connection;
    private readonly Mock<ILogger<HealthController>> _mockLogger;

    public HealthControllerTests()
    {
        (_dbContext, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<HealthController>>();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task GetHealth_ShouldReturnOkWithHealthResponse()
    {
        // Arrange
        var controller = new HealthController(_dbContext, _mockLogger.Object);

        // Act
        var result = await controller.GetHealth();

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        okResult.Should().NotBeNull();
        okResult!.Value.Should().BeOfType<HealthResponseDto>();

        var healthResponse = okResult.Value as HealthResponseDto;
        healthResponse.Should().NotBeNull();
        healthResponse!.Status.Should().Be("Healthy", "database connection is successful");
        healthResponse.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        healthResponse.Version.Should().NotBeNullOrEmpty();
        healthResponse.Checks.Should().ContainKey("Database");
        healthResponse.Checks["Database"].Should().Be("Healthy");
    }

    [Fact]
    public async Task GetHealth_ShouldLogDebugMessage()
    {
        // Arrange
        var controller = new HealthController(_dbContext, _mockLogger.Object);

        // Act
        await controller.GetHealth();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Health check requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a debug log should be written when health check is requested");
    }

    [Fact]
    public async Task GetHealth_ShouldLogInformationMessage()
    {
        // Arrange
        var controller = new HealthController(_dbContext, _mockLogger.Object);

        // Act
        await controller.GetHealth();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Health check completed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "an information log should be written when health check completes");
    }
}
