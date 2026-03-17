using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Middleware;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="CommandLogsController"/>.
/// Tests cover all endpoints: GetLogs, GetCommandStats, GetAnalytics,
/// GetUsageOverTime, GetSuccessRate, and GetPerformance.
/// </summary>
[Trait("Category", "Unit")]
public class CommandLogsControllerTests
{
    private readonly Mock<ICommandLogService> _mockCommandLogService;
    private readonly Mock<ICommandAnalyticsService> _mockAnalyticsService;
    private readonly Mock<ILogger<CommandLogsController>> _mockLogger;
    private readonly CommandLogsController _controller;

    public CommandLogsControllerTests()
    {
        _mockCommandLogService = new Mock<ICommandLogService>();
        _mockAnalyticsService = new Mock<ICommandAnalyticsService>();
        _mockLogger = new Mock<ILogger<CommandLogsController>>();

        _controller = new CommandLogsController(
            _mockCommandLogService.Object,
            _mockAnalyticsService.Object,
            _mockLogger.Object);

        // Setup HttpContext for TraceIdentifier and correlation ID
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.HttpContext.Items[CorrelationIdMiddleware.ItemKey] = "test-correlation-id";
    }

    #region GetLogs Tests

    [Fact]
    public async Task GetLogs_ShouldReturnOkWithPaginatedResults_WhenLogsExist()
    {
        // Arrange
        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>
            {
                CreateTestCommandLogDto(commandName: "ping"),
                CreateTestCommandLogDto(commandName: "status")
            }.AsReadOnly(),
            Page = 1,
            PageSize = 50,
            TotalCount = 2
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _controller.GetLogs(cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as PaginatedResponseDto<CommandLogDto>;

        response.Should().NotBeNull();
        response!.Items.Should().HaveCount(2);
        response.Page.Should().Be(1);
        response.PageSize.Should().Be(50);
        response.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetLogs_ShouldReturnOkWithEmptyList_WhenNoLogsExist()
    {
        // Arrange
        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>().AsReadOnly(),
            Page = 1,
            PageSize = 50,
            TotalCount = 0
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _controller.GetLogs(cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as PaginatedResponseDto<CommandLogDto>;

        response.Should().NotBeNull();
        response!.Items.Should().BeEmpty();
        response.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetLogs_ShouldReturnBadRequest_WhenStartDateIsAfterEndDate()
    {
        // Arrange
        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(-1); // end before start

        // Act
        var result = await _controller.GetLogs(
            startDate: startDate,
            endDate: endDate,
            cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        var badRequestResult = result.Result as BadRequestObjectResult;
        var apiError = badRequestResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Invalid date range");
        apiError.Detail.Should().Be("Start date cannot be after end date.");
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when date range is invalid");
    }

    [Fact]
    public async Task GetLogs_ShouldIncludeCorrelationId_InBadRequestErrorResponse()
    {
        // Arrange
        const string expectedCorrelationId = "my-correlation-id";
        _controller.HttpContext.Items[CorrelationIdMiddleware.ItemKey] = expectedCorrelationId;

        var startDate = DateTime.UtcNow;
        var endDate = DateTime.UtcNow.AddDays(-1);

        // Act
        var result = await _controller.GetLogs(
            startDate: startDate,
            endDate: endDate,
            cancellationToken: CancellationToken.None);

        // Assert
        var badRequestResult = result.Result as BadRequestObjectResult;
        var apiError = badRequestResult!.Value as ApiErrorDto;

        apiError!.TraceId.Should().Be(expectedCorrelationId);
    }

    [Fact]
    public async Task GetLogs_ShouldAllowValidDateRange()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>().AsReadOnly(),
            Page = 1,
            PageSize = 50,
            TotalCount = 0
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _controller.GetLogs(
            startDate: startDate,
            endDate: endDate,
            cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLogs_ShouldPassFilters_ToService()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        const ulong userId = 987654321UL;
        const string commandName = "ping";
        const bool successOnly = true;
        const int page = 2;
        const int pageSize = 25;

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>().AsReadOnly(),
            Page = page,
            PageSize = pageSize,
            TotalCount = 0
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        await _controller.GetLogs(
            guildId: guildId,
            userId: userId,
            commandName: commandName,
            successOnly: successOnly,
            page: page,
            pageSize: pageSize,
            cancellationToken: CancellationToken.None);

        // Assert
        _mockCommandLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<CommandLogQueryDto>(q =>
                    q.GuildId == guildId &&
                    q.UserId == userId &&
                    q.CommandName == commandName &&
                    q.SuccessOnly == successOnly &&
                    q.Page == page &&
                    q.PageSize == pageSize),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLogs_ShouldAllowEqualStartAndEndDates()
    {
        // Arrange
        var date = DateTime.UtcNow.Date;
        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>().AsReadOnly(),
            Page = 1,
            PageSize = 50,
            TotalCount = 0
        };

        _mockCommandLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<CommandLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paginatedResponse);

        // Act
        var result = await _controller.GetLogs(
            startDate: date,
            endDate: date,
            cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region GetCommandStats Tests

    [Fact]
    public async Task GetCommandStats_ShouldReturnOkWithStatsDictionary()
    {
        // Arrange
        var expectedStats = new Dictionary<string, int>
        {
            { "ping", 150 },
            { "status", 75 },
            { "play", 300 }
        };

        _mockCommandLogService
            .Setup(s => s.GetCommandStatsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetCommandStats(cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var stats = okResult!.Value as IDictionary<string, int>;

        stats.Should().NotBeNull();
        stats.Should().HaveCount(3);
        stats!["ping"].Should().Be(150);
        stats["status"].Should().Be(75);
        stats["play"].Should().Be(300);
    }

    [Fact]
    public async Task GetCommandStats_ShouldReturnEmptyDictionary_WhenNoCommandsExecuted()
    {
        // Arrange
        _mockCommandLogService
            .Setup(s => s.GetCommandStatsAsync(It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());

        // Act
        var result = await _controller.GetCommandStats(cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var stats = okResult!.Value as IDictionary<string, int>;

        stats.Should().NotBeNull();
        stats.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCommandStats_ShouldPassSinceParameter_ToService()
    {
        // Arrange
        var since = DateTime.UtcNow.AddDays(-30);

        _mockCommandLogService
            .Setup(s => s.GetCommandStatsAsync(since, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, int>());

        // Act
        await _controller.GetCommandStats(since: since, cancellationToken: CancellationToken.None);

        // Assert
        _mockCommandLogService.Verify(
            s => s.GetCommandStatsAsync(since, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetAnalytics Tests

    [Fact]
    public async Task GetAnalytics_ShouldReturnOkWithAnalyticsData()
    {
        // Arrange
        var analytics = new CommandAnalyticsDto
        {
            TotalCommands = 500,
            SuccessRate = 95.5m,
            AvgResponseTimeMs = 120.0,
            UniqueCommands = 15
        };

        _mockAnalyticsService
            .Setup(s => s.GetAnalyticsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<ulong?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(analytics);

        // Act
        var result = await _controller.GetAnalytics(cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var returnedAnalytics = okResult!.Value as CommandAnalyticsDto;

        returnedAnalytics.Should().NotBeNull();
        returnedAnalytics!.TotalCommands.Should().Be(500);
        returnedAnalytics.SuccessRate.Should().Be(95.5m);
    }

    [Fact]
    public async Task GetAnalytics_ShouldUseDefaultDateRange_WhenNotProvided()
    {
        // Arrange
        var analytics = new CommandAnalyticsDto { TotalCommands = 0 };

        _mockAnalyticsService
            .Setup(s => s.GetAnalyticsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<ulong?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(analytics);

        // Act
        await _controller.GetAnalytics(cancellationToken: CancellationToken.None);

        // Assert — defaults to last 30 days; verify dates are in the expected range
        _mockAnalyticsService.Verify(
            s => s.GetAnalyticsAsync(
                It.Is<DateTime>(d => d > DateTime.UtcNow.AddDays(-31) && d < DateTime.UtcNow.AddDays(-29)),
                It.Is<DateTime>(d => d > DateTime.UtcNow.AddMinutes(-1) && d <= DateTime.UtcNow.AddMinutes(1)),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetAnalytics_ShouldPassGuildId_ToService()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var analytics = new CommandAnalyticsDto { TotalCommands = 0 };

        _mockAnalyticsService
            .Setup(s => s.GetAnalyticsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                guildId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(analytics);

        // Act
        await _controller.GetAnalytics(guildId: guildId, cancellationToken: CancellationToken.None);

        // Assert
        _mockAnalyticsService.Verify(
            s => s.GetAnalyticsAsync(
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                guildId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetUsageOverTime Tests

    [Fact]
    public async Task GetUsageOverTime_ShouldReturnOkWithDataPoints()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;

        var usageData = new List<UsageOverTimeDto>
        {
            new() { Date = start, Count = 10 },
            new() { Date = start.AddDays(1), Count = 15 },
            new() { Date = start.AddDays(2), Count = 8 }
        }.AsReadOnly();

        _mockAnalyticsService
            .Setup(s => s.GetUsageOverTimeAsync(start, end, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(usageData);

        // Act
        var result = await _controller.GetUsageOverTime(start, end, cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var data = okResult!.Value as IEnumerable<UsageOverTimeDto>;

        data.Should().NotBeNull();
        data.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetUsageOverTime_ShouldPassGuildId_ToService()
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(-7);
        var end = DateTime.UtcNow;
        const ulong guildId = 123456789UL;

        _mockAnalyticsService
            .Setup(s => s.GetUsageOverTimeAsync(start, end, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UsageOverTimeDto>().AsReadOnly());

        // Act
        await _controller.GetUsageOverTime(start, end, guildId: guildId, cancellationToken: CancellationToken.None);

        // Assert
        _mockAnalyticsService.Verify(
            s => s.GetUsageOverTimeAsync(start, end, guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetSuccessRate Tests

    [Fact]
    public async Task GetSuccessRate_ShouldReturnOkWithSuccessRateData()
    {
        // Arrange
        var successRateData = new CommandSuccessRateDto
        {
            SuccessCount = 480,
            FailureCount = 20
        };

        _mockAnalyticsService
            .Setup(s => s.GetSuccessRateAsync(It.IsAny<DateTime?>(), It.IsAny<ulong?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(successRateData);

        // Act
        var result = await _controller.GetSuccessRate(cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var returnedData = okResult!.Value as CommandSuccessRateDto;

        returnedData.Should().NotBeNull();
        returnedData!.SuccessCount.Should().Be(480);
        returnedData.FailureCount.Should().Be(20);
        returnedData.TotalCount.Should().Be(500);
        returnedData.SuccessRate.Should().Be(96m);
    }

    [Fact]
    public async Task GetSuccessRate_ShouldPassFilters_ToService()
    {
        // Arrange
        var since = DateTime.UtcNow.AddDays(-14);
        const ulong guildId = 123456789UL;

        _mockAnalyticsService
            .Setup(s => s.GetSuccessRateAsync(since, guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CommandSuccessRateDto { SuccessCount = 0, FailureCount = 0 });

        // Act
        await _controller.GetSuccessRate(since: since, guildId: guildId, cancellationToken: CancellationToken.None);

        // Assert
        _mockAnalyticsService.Verify(
            s => s.GetSuccessRateAsync(since, guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetPerformance Tests

    [Fact]
    public async Task GetPerformance_ShouldReturnOkWithPerformanceMetrics()
    {
        // Arrange
        var performanceData = new List<CommandPerformanceDto>
        {
            new() { CommandName = "play", AvgResponseTimeMs = 250.0, MinResponseTimeMs = 100, MaxResponseTimeMs = 500, ExecutionCount = 200 },
            new() { CommandName = "ping", AvgResponseTimeMs = 50.0, MinResponseTimeMs = 10, MaxResponseTimeMs = 150, ExecutionCount = 500 }
        }.AsReadOnly();

        _mockAnalyticsService
            .Setup(s => s.GetCommandPerformanceAsync(
                It.IsAny<DateTime?>(),
                It.IsAny<ulong?>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(performanceData);

        // Act
        var result = await _controller.GetPerformance(cancellationToken: CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        var okResult = result.Result as OkObjectResult;
        var data = okResult!.Value as IEnumerable<CommandPerformanceDto>;

        data.Should().NotBeNull();
        data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPerformance_ShouldPassLimitParameter_ToService()
    {
        // Arrange
        const int limit = 5;

        _mockAnalyticsService
            .Setup(s => s.GetCommandPerformanceAsync(
                It.IsAny<DateTime?>(),
                It.IsAny<ulong?>(),
                limit,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CommandPerformanceDto>().AsReadOnly());

        // Act
        await _controller.GetPerformance(limit: limit, cancellationToken: CancellationToken.None);

        // Assert
        _mockAnalyticsService.Verify(
            s => s.GetCommandPerformanceAsync(
                It.IsAny<DateTime?>(),
                It.IsAny<ulong?>(),
                limit,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test command log DTO with default values.
    /// </summary>
    private static CommandLogDto CreateTestCommandLogDto(
        string commandName = "ping",
        bool success = true,
        ulong? guildId = null,
        ulong userId = 123456789UL)
    {
        return new CommandLogDto
        {
            Id = Guid.NewGuid(),
            CommandName = commandName,
            UserId = userId,
            Username = "TestUser",
            GuildId = guildId,
            GuildName = guildId.HasValue ? "Test Guild" : null,
            ExecutedAt = DateTime.UtcNow,
            ResponseTimeMs = 100,
            Success = success,
            ErrorMessage = success ? null : "Test error"
        };
    }

    #endregion
}
