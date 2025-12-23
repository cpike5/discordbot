using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CommandAnalyticsService"/>.
/// </summary>
public class CommandAnalyticsServiceTests
{
    private readonly Mock<ICommandLogRepository> _mockCommandLogRepository;
    private readonly Mock<ILogger<CommandAnalyticsService>> _mockLogger;
    private readonly CommandAnalyticsService _service;

    public CommandAnalyticsServiceTests()
    {
        _mockCommandLogRepository = new Mock<ICommandLogRepository>();
        _mockLogger = new Mock<ILogger<CommandAnalyticsService>>();
        _service = new CommandAnalyticsService(_mockCommandLogRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetUsageOverTimeAsync_ShouldReturnDataFromRepository()
    {
        // Arrange
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var expectedData = new List<UsageOverTimeDto>
        {
            new() { Date = start, Count = 10 },
            new() { Date = start.AddDays(1), Count = 15 },
            new() { Date = start.AddDays(2), Count = 20 }
        };

        _mockCommandLogRepository
            .Setup(r => r.GetUsageOverTimeAsync(start, end, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _service.GetUsageOverTimeAsync(start, end);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3, "there are 3 data points");
        result.Should().BeEquivalentTo(expectedData);
    }

    [Fact]
    public async Task GetUsageOverTimeAsync_WithGuildId_ShouldPassParametersCorrectly()
    {
        // Arrange
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var guildId = 111111111UL;
        var expectedData = new List<UsageOverTimeDto>
        {
            new() { Date = start, Count = 5 }
        };

        _mockCommandLogRepository
            .Setup(r => r.GetUsageOverTimeAsync(start, end, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _service.GetUsageOverTimeAsync(start, end, guildId);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        _mockCommandLogRepository.Verify(
            r => r.GetUsageOverTimeAsync(start, end, guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "parameters should be passed through to repository");
    }

    [Fact]
    public async Task GetSuccessRateAsync_ShouldReturnDataFromRepository()
    {
        // Arrange
        var expectedData = new CommandSuccessRateDto
        {
            SuccessCount = 80,
            FailureCount = 20
        };

        _mockCommandLogRepository
            .Setup(r => r.GetSuccessRateAsync(null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _service.GetSuccessRateAsync();

        // Assert
        result.Should().NotBeNull();
        result.SuccessCount.Should().Be(80);
        result.FailureCount.Should().Be(20);
        result.TotalCount.Should().Be(100, "total is success + failure");
        result.SuccessRate.Should().Be(80, "success rate is 80%");
    }

    [Fact]
    public async Task GetSuccessRateAsync_WithOptionalParameters_ShouldPassToRepository()
    {
        // Arrange
        var since = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var guildId = 111111111UL;
        var expectedData = new CommandSuccessRateDto
        {
            SuccessCount = 45,
            FailureCount = 5
        };

        _mockCommandLogRepository
            .Setup(r => r.GetSuccessRateAsync(since, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _service.GetSuccessRateAsync(since, guildId);

        // Assert
        result.Should().NotBeNull();
        result.SuccessCount.Should().Be(45);
        result.FailureCount.Should().Be(5);
        _mockCommandLogRepository.Verify(
            r => r.GetSuccessRateAsync(since, guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "optional parameters should be passed to repository");
    }

    [Fact]
    public async Task GetCommandPerformanceAsync_ShouldReturnDataFromRepository()
    {
        // Arrange
        var expectedData = new List<CommandPerformanceDto>
        {
            new() { CommandName = "ping", AvgResponseTimeMs = 50.5, MinResponseTimeMs = 30, MaxResponseTimeMs = 100, ExecutionCount = 100 },
            new() { CommandName = "status", AvgResponseTimeMs = 120.3, MinResponseTimeMs = 80, MaxResponseTimeMs = 200, ExecutionCount = 50 },
            new() { CommandName = "help", AvgResponseTimeMs = 75.8, MinResponseTimeMs = 50, MaxResponseTimeMs = 150, ExecutionCount = 75 }
        };

        _mockCommandLogRepository
            .Setup(r => r.GetCommandPerformanceAsync(null, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _service.GetCommandPerformanceAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3, "there are 3 commands with performance data");
        result.Should().BeEquivalentTo(expectedData);
    }

    [Fact]
    public async Task GetCommandPerformanceAsync_WithLimitParameter_ShouldRespectLimit()
    {
        // Arrange
        var limit = 5;
        var since = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var guildId = 111111111UL;
        var expectedData = new List<CommandPerformanceDto>
        {
            new() { CommandName = "ping", AvgResponseTimeMs = 50.5, MinResponseTimeMs = 30, MaxResponseTimeMs = 100, ExecutionCount = 100 },
            new() { CommandName = "status", AvgResponseTimeMs = 120.3, MinResponseTimeMs = 80, MaxResponseTimeMs = 200, ExecutionCount = 50 }
        };

        _mockCommandLogRepository
            .Setup(r => r.GetCommandPerformanceAsync(since, guildId, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _service.GetCommandPerformanceAsync(since, guildId, limit);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        _mockCommandLogRepository.Verify(
            r => r.GetCommandPerformanceAsync(since, guildId, limit, It.IsAny<CancellationToken>()),
            Times.Once,
            "limit parameter should be passed to repository");
    }

    [Fact]
    public async Task GetTopCommandsAsync_ShouldReturnDictionaryFromRepository()
    {
        // Arrange
        var allStats = new Dictionary<string, int>
        {
            { "ping", 100 },
            { "status", 80 },
            { "help", 60 },
            { "info", 40 },
            { "config", 20 }
        };

        _mockCommandLogRepository
            .Setup(r => r.GetCommandUsageStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allStats);

        // Act
        var result = await _service.GetTopCommandsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(5, "all 5 commands returned");
        result["ping"].Should().Be(100);
        result["status"].Should().Be(80);
        result["help"].Should().Be(60);
    }

    [Fact]
    public async Task GetTopCommandsAsync_WithLimit_ShouldApplyLimitCorrectly()
    {
        // Arrange
        var limit = 3;
        var allStats = new Dictionary<string, int>
        {
            { "ping", 100 },
            { "status", 80 },
            { "help", 60 },
            { "info", 40 },
            { "config", 20 }
        };

        _mockCommandLogRepository
            .Setup(r => r.GetCommandUsageStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allStats);

        // Act
        var result = await _service.GetTopCommandsAsync(limit: limit);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3, "limit restricts to top 3 commands");
        result.Should().ContainKey("ping", "ping has highest count");
        result.Should().ContainKey("status", "status has second highest count");
        result.Should().ContainKey("help", "help has third highest count");
        result.Should().NotContainKey("info", "info is below the limit");
        result.Should().NotContainKey("config", "config is below the limit");
    }

    [Fact]
    public async Task GetAnalyticsAsync_ShouldAggregateAllDataCorrectly()
    {
        // Arrange
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var guildId = 111111111UL;

        var usageOverTime = new List<UsageOverTimeDto>
        {
            new() { Date = start, Count = 10 },
            new() { Date = start.AddDays(1), Count = 15 },
            new() { Date = start.AddDays(2), Count = 20 }
        };

        var successRate = new CommandSuccessRateDto
        {
            SuccessCount = 40,
            FailureCount = 5
        };

        var performance = new List<CommandPerformanceDto>
        {
            new() { CommandName = "ping", AvgResponseTimeMs = 50.5, MinResponseTimeMs = 30, MaxResponseTimeMs = 100, ExecutionCount = 20 },
            new() { CommandName = "status", AvgResponseTimeMs = 120.3, MinResponseTimeMs = 80, MaxResponseTimeMs = 200, ExecutionCount = 15 },
            new() { CommandName = "help", AvgResponseTimeMs = 75.8, MinResponseTimeMs = 50, MaxResponseTimeMs = 150, ExecutionCount = 10 }
        };

        var topCommands = new Dictionary<string, int>
        {
            { "ping", 20 },
            { "status", 15 },
            { "help", 10 }
        };

        _mockCommandLogRepository
            .Setup(r => r.GetUsageOverTimeAsync(start, end, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageOverTime);

        _mockCommandLogRepository
            .Setup(r => r.GetSuccessRateAsync(start, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(successRate);

        _mockCommandLogRepository
            .Setup(r => r.GetCommandPerformanceAsync(start, guildId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(performance);

        _mockCommandLogRepository
            .Setup(r => r.GetCommandUsageStatsAsync(start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topCommands);

        // Act
        var result = await _service.GetAnalyticsAsync(start, end, guildId);

        // Assert
        result.Should().NotBeNull();
        result.TotalCommands.Should().Be(45, "total commands is sum of usage over time counts (10 + 15 + 20)");
        result.UniqueCommands.Should().Be(3, "there are 3 unique commands in top commands");
        result.SuccessRate.Should().Be(successRate.SuccessRate);
        result.AvgResponseTimeMs.Should().BeApproximately(82.2, 0.1, "average of command performance averages");
        result.UsageOverTime.Should().BeEquivalentTo(usageOverTime);
        result.TopCommands.Should().BeEquivalentTo(topCommands);
        result.SuccessRateData.Should().BeEquivalentTo(successRate);
        result.PerformanceData.Should().BeEquivalentTo(performance);
    }

    [Fact]
    public async Task GetAnalyticsAsync_WithEmptyData_ShouldHandleGracefully()
    {
        // Arrange
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var emptyUsageOverTime = new List<UsageOverTimeDto>();
        var emptySuccessRate = new CommandSuccessRateDto
        {
            SuccessCount = 0,
            FailureCount = 0
        };
        var emptyPerformance = new List<CommandPerformanceDto>();
        var emptyTopCommands = new Dictionary<string, int>();

        _mockCommandLogRepository
            .Setup(r => r.GetUsageOverTimeAsync(start, end, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyUsageOverTime);

        _mockCommandLogRepository
            .Setup(r => r.GetSuccessRateAsync(start, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptySuccessRate);

        _mockCommandLogRepository
            .Setup(r => r.GetCommandPerformanceAsync(start, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyPerformance);

        _mockCommandLogRepository
            .Setup(r => r.GetCommandUsageStatsAsync(start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyTopCommands);

        // Act
        var result = await _service.GetAnalyticsAsync(start, end);

        // Assert
        result.Should().NotBeNull();
        result.TotalCommands.Should().Be(0, "no commands executed");
        result.UniqueCommands.Should().Be(0, "no unique commands");
        result.SuccessRate.Should().Be(0, "success rate is 0 when no commands");
        result.AvgResponseTimeMs.Should().Be(0, "average response time is 0 when no performance data");
        result.UsageOverTime.Should().BeEmpty();
        result.TopCommands.Should().BeEmpty();
        result.PerformanceData.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAnalyticsAsync_ShouldFetchDataInParallel()
    {
        // Arrange
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        var usageOverTime = new List<UsageOverTimeDto> { new() { Date = start, Count = 10 } };
        var successRate = new CommandSuccessRateDto { SuccessCount = 10, FailureCount = 0 };
        var performance = new List<CommandPerformanceDto> { new() { CommandName = "ping", AvgResponseTimeMs = 50, MinResponseTimeMs = 30, MaxResponseTimeMs = 100, ExecutionCount = 10 } };
        var topCommands = new Dictionary<string, int> { { "ping", 10 } };

        _mockCommandLogRepository
            .Setup(r => r.GetUsageOverTimeAsync(start, end, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageOverTime);

        _mockCommandLogRepository
            .Setup(r => r.GetSuccessRateAsync(start, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(successRate);

        _mockCommandLogRepository
            .Setup(r => r.GetCommandPerformanceAsync(start, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(performance);

        _mockCommandLogRepository
            .Setup(r => r.GetCommandUsageStatsAsync(start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topCommands);

        // Act
        var result = await _service.GetAnalyticsAsync(start, end);

        // Assert
        result.Should().NotBeNull();
        _mockCommandLogRepository.Verify(r => r.GetUsageOverTimeAsync(start, end, null, It.IsAny<CancellationToken>()), Times.Once);
        _mockCommandLogRepository.Verify(r => r.GetSuccessRateAsync(start, null, It.IsAny<CancellationToken>()), Times.Once);
        _mockCommandLogRepository.Verify(r => r.GetCommandPerformanceAsync(start, null, 10, It.IsAny<CancellationToken>()), Times.Once);
        _mockCommandLogRepository.Verify(r => r.GetCommandUsageStatsAsync(start, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTopCommandsAsync_ShouldOrderByCountDescending()
    {
        // Arrange
        var allStats = new Dictionary<string, int>
        {
            { "config", 20 },
            { "ping", 100 },
            { "help", 60 },
            { "status", 80 },
            { "info", 40 }
        };

        _mockCommandLogRepository
            .Setup(r => r.GetCommandUsageStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allStats);

        // Act
        var result = await _service.GetTopCommandsAsync(limit: 3);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        var commands = result.Keys.ToList();
        commands[0].Should().Be("ping", "ping has the highest count (100)");
        commands[1].Should().Be("status", "status has the second highest count (80)");
        commands[2].Should().Be("help", "help has the third highest count (60)");
    }

    [Fact]
    public async Task GetUsageOverTimeAsync_WithCancellationToken_ShouldPassToRepository()
    {
        // Arrange
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockCommandLogRepository
            .Setup(r => r.GetUsageOverTimeAsync(start, end, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UsageOverTimeDto>());

        // Act
        await _service.GetUsageOverTimeAsync(start, end, cancellationToken: cancellationToken);

        // Assert
        _mockCommandLogRepository.Verify(
            r => r.GetUsageOverTimeAsync(start, end, null, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    [Fact]
    public async Task GetSuccessRateAsync_WithCancellationToken_ShouldPassToRepository()
    {
        // Arrange
        var since = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var guildId = 111111111UL;
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockCommandLogRepository
            .Setup(r => r.GetSuccessRateAsync(since, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandSuccessRateDto());

        // Act
        await _service.GetSuccessRateAsync(since, guildId, cancellationToken);

        // Assert
        _mockCommandLogRepository.Verify(
            r => r.GetSuccessRateAsync(since, guildId, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    [Fact]
    public async Task GetCommandPerformanceAsync_WithCancellationToken_ShouldPassToRepository()
    {
        // Arrange
        var since = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var guildId = 111111111UL;
        var limit = 15;
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockCommandLogRepository
            .Setup(r => r.GetCommandPerformanceAsync(since, guildId, limit, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CommandPerformanceDto>());

        // Act
        await _service.GetCommandPerformanceAsync(since, guildId, limit, cancellationToken);

        // Assert
        _mockCommandLogRepository.Verify(
            r => r.GetCommandPerformanceAsync(since, guildId, limit, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    [Fact]
    public async Task GetTopCommandsAsync_WithCancellationToken_ShouldPassToRepository()
    {
        // Arrange
        var since = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockCommandLogRepository
            .Setup(r => r.GetCommandUsageStatsAsync(since, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());

        // Act
        await _service.GetTopCommandsAsync(since, cancellationToken: cancellationToken);

        // Assert
        _mockCommandLogRepository.Verify(
            r => r.GetCommandUsageStatsAsync(since, cancellationToken),
            Times.Once,
            "cancellation token should be passed to repository");
    }

    [Fact]
    public async Task GetAnalyticsAsync_WithCancellationToken_ShouldPassToAllRepositoryCalls()
    {
        // Arrange
        var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2023, 1, 31, 23, 59, 59, DateTimeKind.Utc);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockCommandLogRepository
            .Setup(r => r.GetUsageOverTimeAsync(start, end, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UsageOverTimeDto>());

        _mockCommandLogRepository
            .Setup(r => r.GetSuccessRateAsync(start, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandSuccessRateDto());

        _mockCommandLogRepository
            .Setup(r => r.GetCommandPerformanceAsync(start, null, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CommandPerformanceDto>());

        _mockCommandLogRepository
            .Setup(r => r.GetCommandUsageStatsAsync(start, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());

        // Act
        await _service.GetAnalyticsAsync(start, end, cancellationToken: cancellationToken);

        // Assert
        _mockCommandLogRepository.Verify(
            r => r.GetUsageOverTimeAsync(start, end, null, cancellationToken),
            Times.Once,
            "cancellation token should be passed to GetUsageOverTimeAsync");

        _mockCommandLogRepository.Verify(
            r => r.GetSuccessRateAsync(start, null, cancellationToken),
            Times.Once,
            "cancellation token should be passed to GetSuccessRateAsync");

        _mockCommandLogRepository.Verify(
            r => r.GetCommandPerformanceAsync(start, null, 10, cancellationToken),
            Times.Once,
            "cancellation token should be passed to GetCommandPerformanceAsync");

        _mockCommandLogRepository.Verify(
            r => r.GetCommandUsageStatsAsync(start, cancellationToken),
            Times.Once,
            "cancellation token should be passed to GetCommandUsageStatsAsync");
    }
}
