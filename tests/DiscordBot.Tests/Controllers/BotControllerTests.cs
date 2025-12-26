using DiscordBot.Bot.Controllers;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="BotController"/>.
/// </summary>
public class BotControllerTests
{
    private readonly Mock<IBotService> _mockBotService;
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<ICommandLogService> _mockCommandLogService;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<BotController>> _mockLogger;
    private readonly BotController _controller;

    public BotControllerTests()
    {
        _mockBotService = new Mock<IBotService>();
        _mockGuildService = new Mock<IGuildService>();
        _mockCommandLogService = new Mock<ICommandLogService>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _mockLogger = new Mock<ILogger<BotController>>();

        var cachingOptions = Options.Create(new CachingOptions
        {
            DashboardStatsCacheDurationSeconds = 5
        });

        _controller = new BotController(
            _mockBotService.Object,
            _mockGuildService.Object,
            _mockCommandLogService.Object,
            _cache,
            cachingOptions,
            _mockLogger.Object);

        // Setup HttpContext for TraceIdentifier
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region GetStatus Tests

    [Fact]
    public void GetStatus_ShouldReturnOkWithBotStatus()
    {
        // Arrange
        var expectedStatus = new BotStatusDto
        {
            Uptime = TimeSpan.FromHours(5),
            GuildCount = 10,
            LatencyMs = 42,
            ConnectionState = "Connected",
            BotUsername = "TestBot",
            StartTime = DateTime.UtcNow.AddHours(-5)
        };

        _mockBotService.Setup(s => s.GetStatus()).Returns(expectedStatus);

        // Act
        var result = _controller.GetStatus();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeSameAs(expectedStatus);
    }

    #endregion

    #region GetConnectedGuilds Tests

    [Fact]
    public void GetConnectedGuilds_ShouldReturnOkWithGuildList()
    {
        // Arrange
        var expectedGuilds = new List<GuildInfoDto>
        {
            new() { Id = 123456789, Name = "Test Guild 1", MemberCount = 100 },
            new() { Id = 987654321, Name = "Test Guild 2", MemberCount = 50 }
        };

        _mockBotService.Setup(s => s.GetConnectedGuilds()).Returns(expectedGuilds);

        // Act
        var result = _controller.GetConnectedGuilds();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeSameAs(expectedGuilds);
    }

    #endregion

    #region GetDashboardStats Tests

    [Fact]
    public async Task GetDashboardStats_ShouldReturnAggregatedStats()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        var result = await _controller.GetDashboardStats(CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var stats = okResult!.Value as DashboardAggregatedDto;

        stats.Should().NotBeNull();
        stats!.BotStatus.ConnectionState.Should().Be("Connected");
        stats.BotStatus.GuildCount.Should().Be(5);
        stats.GuildStats.TotalGuilds.Should().Be(2);
        stats.GuildStats.TotalMembers.Should().Be(150);
        stats.CommandStats.TotalCommands.Should().Be(25);
        stats.RecentActivity.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetDashboardStats_ShouldCacheResult()
    {
        // Arrange
        SetupDefaultMocks();

        // Act - First call
        await _controller.GetDashboardStats(CancellationToken.None);

        // Act - Second call (should use cache)
        await _controller.GetDashboardStats(CancellationToken.None);

        // Assert - Services should only be called once
        _mockBotService.Verify(s => s.GetStatus(), Times.Once);
        _mockGuildService.Verify(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCommandLogService.Verify(s => s.GetCommandStatsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockCommandLogService.Verify(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetDashboardStats_ShouldReturnCachedResult()
    {
        // Arrange
        SetupDefaultMocks();

        // Act - First call
        var firstResult = await _controller.GetDashboardStats(CancellationToken.None);

        // Modify mock to return different data
        _mockBotService.Setup(s => s.GetStatus()).Returns(new BotStatusDto
        {
            ConnectionState = "Disconnected",
            GuildCount = 0
        });

        // Act - Second call (should return cached result, not new data)
        var secondResult = await _controller.GetDashboardStats(CancellationToken.None);

        // Assert - Both results should be the same (from cache)
        var firstStats = (firstResult.Result as OkObjectResult)?.Value as DashboardAggregatedDto;
        var secondStats = (secondResult.Result as OkObjectResult)?.Value as DashboardAggregatedDto;

        secondStats!.BotStatus.ConnectionState.Should().Be("Connected");
        secondStats.BotStatus.GuildCount.Should().Be(5);
    }

    [Fact]
    public async Task GetDashboardStats_ShouldIncludeBotStatus()
    {
        // Arrange
        var botStatus = new BotStatusDto
        {
            Uptime = TimeSpan.FromHours(10),
            GuildCount = 15,
            LatencyMs = 35,
            ConnectionState = "Connected",
            BotUsername = "TestBot",
            StartTime = DateTime.UtcNow.AddHours(-10)
        };

        _mockBotService.Setup(s => s.GetStatus()).Returns(botStatus);
        _mockGuildService.Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>().AsReadOnly());
        _mockCommandLogService.Setup(s => s.GetCommandStatsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());
        _mockCommandLogService.Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<CommandLogDto> { Items = new List<CommandLogDto>() });

        // Act
        var result = await _controller.GetDashboardStats(CancellationToken.None);

        // Assert
        var stats = (result.Result as OkObjectResult)?.Value as DashboardAggregatedDto;
        stats!.BotStatus.ConnectionState.Should().Be("Connected");
        stats.BotStatus.Latency.Should().Be(35);
        stats.BotStatus.GuildCount.Should().Be(15);
        stats.BotStatus.Uptime.Should().Be(TimeSpan.FromHours(10));
    }

    [Fact]
    public async Task GetDashboardStats_ShouldCalculateTotalMembers()
    {
        // Arrange
        _mockBotService.Setup(s => s.GetStatus()).Returns(new BotStatusDto { ConnectionState = "Connected" });
        _mockGuildService.Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>
            {
                new() { Id = 1, Name = "Guild 1", MemberCount = 100 },
                new() { Id = 2, Name = "Guild 2", MemberCount = 200 },
                new() { Id = 3, Name = "Guild 3", MemberCount = 300 }
            }.AsReadOnly());
        _mockCommandLogService.Setup(s => s.GetCommandStatsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());
        _mockCommandLogService.Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<CommandLogDto> { Items = new List<CommandLogDto>() });

        // Act
        var result = await _controller.GetDashboardStats(CancellationToken.None);

        // Assert
        var stats = (result.Result as OkObjectResult)?.Value as DashboardAggregatedDto;
        stats!.GuildStats.TotalGuilds.Should().Be(3);
        stats.GuildStats.TotalMembers.Should().Be(600);
    }

    [Fact]
    public async Task GetDashboardStats_ShouldIncludeCommandStats()
    {
        // Arrange
        _mockBotService.Setup(s => s.GetStatus()).Returns(new BotStatusDto { ConnectionState = "Connected" });
        _mockGuildService.Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>().AsReadOnly());
        _mockCommandLogService.Setup(s => s.GetCommandStatsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>
            {
                { "ping", 50 },
                { "help", 30 },
                { "info", 20 }
            });
        _mockCommandLogService.Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<CommandLogDto>
            {
                Items = new List<CommandLogDto>
                {
                    new() { Id = Guid.NewGuid(), CommandName = "ping", Success = true },
                    new() { Id = Guid.NewGuid(), CommandName = "help", Success = true },
                    new() { Id = Guid.NewGuid(), CommandName = "fail", Success = false }
                }
            });

        // Act
        var result = await _controller.GetDashboardStats(CancellationToken.None);

        // Assert
        var stats = (result.Result as OkObjectResult)?.Value as DashboardAggregatedDto;
        stats!.CommandStats.TotalCommands.Should().Be(100);
        stats.CommandStats.SuccessfulCommands.Should().Be(2);
        stats.CommandStats.FailedCommands.Should().Be(1);
        stats.CommandStats.CommandUsage.Should().ContainKey("ping");
        stats.CommandStats.CommandUsage["ping"].Should().Be(50);
    }

    [Fact]
    public async Task GetDashboardStats_ShouldIncludeRecentActivity()
    {
        // Arrange
        var commandLog = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            CommandName = "ping",
            ExecutedAt = DateTime.UtcNow.AddMinutes(-5),
            GuildId = 123456789,
            GuildName = "Test Guild",
            UserId = 987654321,
            Username = "TestUser",
            Success = true
        };

        _mockBotService.Setup(s => s.GetStatus()).Returns(new BotStatusDto { ConnectionState = "Connected" });
        _mockGuildService.Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>().AsReadOnly());
        _mockCommandLogService.Setup(s => s.GetCommandStatsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());
        _mockCommandLogService.Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<CommandLogDto> { Items = new List<CommandLogDto> { commandLog } });

        // Act
        var result = await _controller.GetDashboardStats(CancellationToken.None);

        // Assert
        var stats = (result.Result as OkObjectResult)?.Value as DashboardAggregatedDto;
        stats!.RecentActivity.Should().HaveCount(1);

        var activity = stats.RecentActivity[0];
        activity.Id.Should().Be(commandLog.Id);
        activity.Type.Should().Be("CommandExecuted");
        activity.Description.Should().Be("/ping");
        activity.GuildId.Should().Be(123456789UL);
        activity.GuildName.Should().Be("Test Guild");
        activity.UserId.Should().Be(987654321UL);
        activity.Username.Should().Be("TestUser");
        activity.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetDashboardStats_ShouldRequestLast10RecentActivities()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        await _controller.GetDashboardStats(CancellationToken.None);

        // Assert
        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q => q.PageSize == 10 && q.Page == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetDashboardStats_ShouldIncludeTimestamp()
    {
        // Arrange
        SetupDefaultMocks();
        var beforeCall = DateTime.UtcNow;

        // Act
        var result = await _controller.GetDashboardStats(CancellationToken.None);
        var afterCall = DateTime.UtcNow;

        // Assert
        var stats = (result.Result as OkObjectResult)?.Value as DashboardAggregatedDto;
        stats!.Timestamp.Should().BeOnOrAfter(beforeCall);
        stats.Timestamp.Should().BeOnOrBefore(afterCall);
        stats.BotStatus.Timestamp.Should().BeOnOrAfter(beforeCall);
        stats.BotStatus.Timestamp.Should().BeOnOrBefore(afterCall);
    }

    #endregion

    #region Helper Methods

    private void SetupDefaultMocks()
    {
        _mockBotService.Setup(s => s.GetStatus()).Returns(new BotStatusDto
        {
            Uptime = TimeSpan.FromHours(5),
            GuildCount = 5,
            LatencyMs = 42,
            ConnectionState = "Connected",
            BotUsername = "TestBot",
            StartTime = DateTime.UtcNow.AddHours(-5)
        });

        _mockGuildService.Setup(s => s.GetAllGuildsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<GuildDto>
            {
                new() { Id = 1, Name = "Guild 1", MemberCount = 100 },
                new() { Id = 2, Name = "Guild 2", MemberCount = 50 }
            }.AsReadOnly());

        _mockCommandLogService.Setup(s => s.GetCommandStatsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>
            {
                { "ping", 15 },
                { "help", 10 }
            });

        _mockCommandLogService.Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedResponseDto<CommandLogDto>
            {
                Items = new List<CommandLogDto>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        CommandName = "ping",
                        ExecutedAt = DateTime.UtcNow.AddMinutes(-1),
                        GuildId = 123456789,
                        GuildName = "Test Guild",
                        UserId = 987654321,
                        Username = "TestUser",
                        Success = true
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        CommandName = "help",
                        ExecutedAt = DateTime.UtcNow.AddMinutes(-5),
                        GuildId = 123456789,
                        GuildName = "Test Guild",
                        UserId = 111111111,
                        Username = "AnotherUser",
                        Success = true
                    }
                },
                TotalCount = 2,
                Page = 1,
                PageSize = 10
            });
    }

    #endregion
}
