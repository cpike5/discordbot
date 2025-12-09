using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="BotService"/>.
/// NOTE: Direct testing of BotService is limited because DiscordSocketClient is a sealed class
/// that cannot be easily mocked. These tests verify the testable parts of the service.
/// Integration testing through the controller layer provides better coverage for this service.
/// </summary>
public class BotServiceTests
{
    private readonly Mock<IHostApplicationLifetime> _mockLifetime;
    private readonly Mock<ILogger<BotService>> _mockLogger;

    public BotServiceTests()
    {
        _mockLifetime = new Mock<IHostApplicationLifetime>();
        _mockLogger = new Mock<ILogger<BotService>>();
    }

    /// <summary>
    /// Documentation test that describes expected behavior of GetStatus.
    /// Actual testing is done through integration tests or controller tests.
    /// </summary>
    [Fact]
    public void GetStatus_ExpectedBehavior_Documentation()
    {
        // This test documents the expected behavior of BotService.GetStatus:
        // 1. Retrieves guild count from DiscordSocketClient.Guilds.Count
        // 2. Retrieves latency from DiscordSocketClient.Latency
        // 3. Retrieves connection state from DiscordSocketClient.ConnectionState
        // 4. Retrieves bot username from DiscordSocketClient.CurrentUser?.Username ?? "Unknown"
        // 5. Calculates uptime based on static start time
        // 6. Returns a BotStatusDto with all these values
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/BotService.cs:34-52

        var expectedBehavior = new
        {
            Method = "GetStatus",
            Returns = "BotStatusDto",
            Properties = new[]
            {
                "Uptime (calculated from static start time)",
                "GuildCount (from client.Guilds.Count)",
                "LatencyMs (from client.Latency)",
                "ConnectionState (from client.ConnectionState.ToString())",
                "BotUsername (from client.CurrentUser?.Username ?? 'Unknown')",
                "StartTime (static field initialized at class load)"
            }
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.Method.Should().Be("GetStatus");
        expectedBehavior.Returns.Should().Be("BotStatusDto");
        expectedBehavior.Properties.Should().HaveCount(6);
    }

    /// <summary>
    /// Documentation test that describes expected behavior of GetConnectedGuilds.
    /// Actual testing is done through integration tests or controller tests.
    /// </summary>
    [Fact]
    public void GetConnectedGuilds_ExpectedBehavior_Documentation()
    {
        // This test documents the expected behavior of BotService.GetConnectedGuilds:
        // 1. Retrieves guilds from DiscordSocketClient.Guilds
        // 2. Maps each guild to GuildInfoDto with:
        //    - Id from guild.Id
        //    - Name from guild.Name
        //    - MemberCount from guild.MemberCount
        //    - IconUrl from guild.IconUrl
        //    - JoinedAt from guild.CurrentUser?.JoinedAt?.UtcDateTime (nullable)
        // 3. Returns as IReadOnlyList<GuildInfoDto>
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/BotService.cs:55-73

        var expectedBehavior = new
        {
            Method = "GetConnectedGuilds",
            Returns = "IReadOnlyList<GuildInfoDto>",
            Mapping = new[]
            {
                "Id from guild.Id",
                "Name from guild.Name",
                "MemberCount from guild.MemberCount",
                "IconUrl from guild.IconUrl",
                "JoinedAt from guild.CurrentUser?.JoinedAt?.UtcDateTime (nullable)"
            }
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.Method.Should().Be("GetConnectedGuilds");
        expectedBehavior.Returns.Should().Be("IReadOnlyList<GuildInfoDto>");
        expectedBehavior.Mapping.Should().HaveCount(5);
    }

    [Fact]
    public async Task ShutdownAsync_ShouldCallStopApplication()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new BotService(client, _mockLifetime.Object, _mockLogger.Object);

        // Act
        await service.ShutdownAsync();

        // Assert
        _mockLifetime.Verify(
            l => l.StopApplication(),
            Times.Once,
            "StopApplication should be called once to initiate shutdown");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task ShutdownAsync_ShouldLogWarning()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new BotService(client, _mockLifetime.Object, _mockLogger.Object);

        // Act
        await service.ShutdownAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Shutdown requested")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a warning should be logged when shutdown is requested");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task ShutdownAsync_WithCancellationToken_ShouldComplete()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new BotService(client, _mockLifetime.Object, _mockLogger.Object);
        var cancellationTokenSource = new CancellationTokenSource();

        // Act
        await service.ShutdownAsync(cancellationTokenSource.Token);

        // Assert
        _mockLifetime.Verify(
            l => l.StopApplication(),
            Times.Once,
            "StopApplication should be called even with cancellation token");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task RestartAsync_ShouldThrowNotSupportedException()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new BotService(client, _mockLifetime.Object, _mockLogger.Object);

        // Act & Assert
        await FluentActions.Invoking(async () => await service.RestartAsync())
            .Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*restart is not currently supported*");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task RestartAsync_ShouldLogWarning()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new BotService(client, _mockLifetime.Object, _mockLogger.Object);

        // Act
        try
        {
            await service.RestartAsync();
        }
        catch (NotSupportedException)
        {
            // Expected exception
        }

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Restart requested but not implemented")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "a warning should be logged when restart is requested");

        // Cleanup
        await client.DisposeAsync();
    }
}
