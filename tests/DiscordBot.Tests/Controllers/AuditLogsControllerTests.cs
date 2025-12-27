using DiscordBot.Bot.Controllers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="AuditLogsController"/>.
/// Tests cover all endpoints: GetLogs, GetById, GetStats, and GetByCorrelationId.
/// </summary>
[Trait("Category", "Unit")]
public class AuditLogsControllerTests
{
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<ILogger<AuditLogsController>> _mockLogger;
    private readonly AuditLogsController _controller;

    public AuditLogsControllerTests()
    {
        _mockAuditLogService = new Mock<IAuditLogService>();
        _mockLogger = new Mock<ILogger<AuditLogsController>>();

        _controller = new AuditLogsController(
            _mockAuditLogService.Object,
            _mockLogger.Object);

        // Setup HttpContext for TraceIdentifier and correlation ID
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region GetLogs Tests

    [Fact]
    public async Task GetLogs_ShouldReturnPaginatedResults_WhenLogsExist()
    {
        // Arrange
        var query = new AuditLogQueryDto { Page = 1, PageSize = 20 };
        var logs = new List<AuditLogDto>
        {
            CreateTestAuditLogDto(1),
            CreateTestAuditLogDto(2),
            CreateTestAuditLogDto(3)
        };

        _mockAuditLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((logs, 3));

        // Act
        var result = await _controller.GetLogs(query, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var paginatedResponse = okResult!.Value as PaginatedResponseDto<AuditLogDto>;

        paginatedResponse.Should().NotBeNull();
        paginatedResponse!.Items.Should().HaveCount(3);
        paginatedResponse.Page.Should().Be(1);
        paginatedResponse.PageSize.Should().Be(20);
        paginatedResponse.TotalCount.Should().Be(3);

        _mockAuditLogService.Verify(
            s => s.GetLogsAsync(query, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLogs_ShouldReturnEmptyList_WhenNoLogsExist()
    {
        // Arrange
        var query = new AuditLogQueryDto { Page = 1, PageSize = 20 };

        _mockAuditLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogDto>(), 0));

        // Act
        var result = await _controller.GetLogs(query, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var paginatedResponse = okResult!.Value as PaginatedResponseDto<AuditLogDto>;

        paginatedResponse.Should().NotBeNull();
        paginatedResponse!.Items.Should().BeEmpty();
        paginatedResponse.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetLogs_ShouldReturnBadRequest_WhenStartDateAfterEndDate()
    {
        // Arrange
        var query = new AuditLogQueryDto
        {
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(-1), // End date before start date
            Page = 1,
            PageSize = 20
        };

        // Act
        var result = await _controller.GetLogs(query, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result.Result as BadRequestObjectResult;
        var apiError = badRequestResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Invalid date range");
        apiError.Detail.Should().Be("Start date cannot be after end date.");
        apiError.StatusCode.Should().Be(StatusCodes.Status400BadRequest);

        _mockAuditLogService.Verify(
            s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "service should not be called when date range is invalid");
    }

    [Fact]
    public async Task GetLogs_ShouldAllowValidDateRange()
    {
        // Arrange
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var query = new AuditLogQueryDto
        {
            StartDate = startDate,
            EndDate = endDate,
            Page = 1,
            PageSize = 20
        };

        _mockAuditLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogDto>(), 0));

        // Act
        var result = await _controller.GetLogs(query, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        _mockAuditLogService.Verify(
            s => s.GetLogsAsync(query, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLogs_ShouldAllowEqualStartAndEndDates()
    {
        // Arrange
        var date = DateTime.UtcNow.Date;
        var query = new AuditLogQueryDto
        {
            StartDate = date,
            EndDate = date,
            Page = 1,
            PageSize = 20
        };

        _mockAuditLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogDto>(), 0));

        // Act
        var result = await _controller.GetLogs(query, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        _mockAuditLogService.Verify(
            s => s.GetLogsAsync(query, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLogs_ShouldAllowNullDates()
    {
        // Arrange
        var query = new AuditLogQueryDto
        {
            StartDate = null,
            EndDate = null,
            Page = 1,
            PageSize = 20
        };

        _mockAuditLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogDto>(), 0));

        // Act
        var result = await _controller.GetLogs(query, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();

        _mockAuditLogService.Verify(
            s => s.GetLogsAsync(query, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLogs_ShouldUseDefaultPagination_WhenNotSpecified()
    {
        // Arrange
        var query = new AuditLogQueryDto(); // No explicit page/pageSize set

        _mockAuditLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogDto>(), 0));

        // Act
        var result = await _controller.GetLogs(query, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var paginatedResponse = okResult!.Value as PaginatedResponseDto<AuditLogDto>;

        paginatedResponse!.Page.Should().Be(1);
        paginatedResponse.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task GetLogs_ShouldPassAllFilters_ToCategoryFilter()
    {
        // Arrange
        var query = new AuditLogQueryDto
        {
            Category = AuditLogCategory.Guild,
            Page = 1,
            PageSize = 20
        };

        _mockAuditLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogDto>(), 0));

        // Act
        await _controller.GetLogs(query, CancellationToken.None);

        // Assert
        _mockAuditLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<AuditLogQueryDto>(q => q.Category == AuditLogCategory.Guild),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLogs_ShouldPassAllFilters_ToActionFilter()
    {
        // Arrange
        var query = new AuditLogQueryDto
        {
            Action = AuditLogAction.Created,
            Page = 1,
            PageSize = 20
        };

        _mockAuditLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogDto>(), 0));

        // Act
        await _controller.GetLogs(query, CancellationToken.None);

        // Assert
        _mockAuditLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<AuditLogQueryDto>(q => q.Action == AuditLogAction.Created),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLogs_ShouldPassAllFilters_ToActorIdFilter()
    {
        // Arrange
        var query = new AuditLogQueryDto
        {
            ActorId = "user123",
            Page = 1,
            PageSize = 20
        };

        _mockAuditLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogDto>(), 0));

        // Act
        await _controller.GetLogs(query, CancellationToken.None);

        // Assert
        _mockAuditLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<AuditLogQueryDto>(q => q.ActorId == "user123"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLogs_ShouldPassAllFilters_ToGuildIdFilter()
    {
        // Arrange
        var query = new AuditLogQueryDto
        {
            GuildId = 123456789UL,
            Page = 1,
            PageSize = 20
        };

        _mockAuditLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogDto>(), 0));

        // Act
        await _controller.GetLogs(query, CancellationToken.None);

        // Assert
        _mockAuditLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<AuditLogQueryDto>(q => q.GuildId == 123456789UL),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLogs_ShouldPassCombinedFilters_ToService()
    {
        // Arrange
        var query = new AuditLogQueryDto
        {
            Category = AuditLogCategory.User,
            Action = AuditLogAction.Updated,
            ActorId = "admin123",
            GuildId = 999888777UL,
            Page = 2,
            PageSize = 50
        };

        _mockAuditLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLogDto>(), 0));

        // Act
        await _controller.GetLogs(query, CancellationToken.None);

        // Assert
        _mockAuditLogService.Verify(
            s => s.GetLogsAsync(
                It.Is<AuditLogQueryDto>(q =>
                    q.Category == AuditLogCategory.User &&
                    q.Action == AuditLogAction.Updated &&
                    q.ActorId == "admin123" &&
                    q.GuildId == 999888777UL &&
                    q.Page == 2 &&
                    q.PageSize == 50),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLogs_ShouldCalculateTotalPages_InResponse()
    {
        // Arrange
        var query = new AuditLogQueryDto { Page = 1, PageSize = 10 };
        var logs = new List<AuditLogDto>
        {
            CreateTestAuditLogDto(1),
            CreateTestAuditLogDto(2)
        };

        _mockAuditLogService
            .Setup(s => s.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((logs, 25)); // Total count of 25 items

        // Act
        var result = await _controller.GetLogs(query, CancellationToken.None);

        // Assert
        var okResult = result.Result as OkObjectResult;
        var paginatedResponse = okResult!.Value as PaginatedResponseDto<AuditLogDto>;

        paginatedResponse!.TotalPages.Should().Be(3); // 25 items / 10 per page = 3 pages
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetById_ShouldReturnLog_WhenFound()
    {
        // Arrange
        const long logId = 123;
        var log = CreateTestAuditLogDto(logId);

        _mockAuditLogService
            .Setup(s => s.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);

        // Act
        var result = await _controller.GetById(logId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedLog = okResult!.Value as AuditLogDto;

        returnedLog.Should().NotBeNull();
        returnedLog!.Id.Should().Be(logId);
        returnedLog.Category.Should().Be(AuditLogCategory.Guild);
        returnedLog.Action.Should().Be(AuditLogAction.Updated);

        _mockAuditLogService.Verify(
            s => s.GetByIdAsync(logId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenLogDoesNotExist()
    {
        // Arrange
        const long logId = 999;

        _mockAuditLogService
            .Setup(s => s.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLogDto?)null);

        // Act
        var result = await _controller.GetById(logId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = result.Result as NotFoundObjectResult;
        var apiError = notFoundResult!.Value as ApiErrorDto;

        apiError.Should().NotBeNull();
        apiError!.Message.Should().Be("Audit log not found");
        apiError.Detail.Should().Be($"No audit log entry with ID {logId} exists in the database.");
        apiError.StatusCode.Should().Be(StatusCodes.Status404NotFound);

        _mockAuditLogService.Verify(
            s => s.GetByIdAsync(logId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetById_ShouldIncludeCorrelationId_InErrorResponse()
    {
        // Arrange
        const long logId = 999;
        const string expectedTraceId = "test-trace-id-123";

        _controller.ControllerContext.HttpContext.TraceIdentifier = expectedTraceId;

        _mockAuditLogService
            .Setup(s => s.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLogDto?)null);

        // Act
        var result = await _controller.GetById(logId, CancellationToken.None);

        // Assert
        var notFoundResult = result.Result as NotFoundObjectResult;
        var apiError = notFoundResult!.Value as ApiErrorDto;

        apiError!.TraceId.Should().Be(expectedTraceId);
    }

    #endregion

    #region GetStats Tests

    [Fact]
    public async Task GetStats_ShouldReturnGlobalStats_WhenGuildIdIsNull()
    {
        // Arrange
        var expectedStats = new AuditLogStatsDto
        {
            TotalEntries = 5000,
            Last24Hours = 100,
            Last7Days = 400,
            Last30Days = 1200,
            ByCategory = new Dictionary<AuditLogCategory, int>
            {
                { AuditLogCategory.Guild, 2000 },
                { AuditLogCategory.User, 1500 },
                { AuditLogCategory.System, 1500 }
            },
            ByAction = new Dictionary<AuditLogAction, int>
            {
                { AuditLogAction.Created, 1000 },
                { AuditLogAction.Updated, 2000 },
                { AuditLogAction.Deleted, 500 }
            },
            TopActors = new Dictionary<string, int>
            {
                { "user123", 50 },
                { "user456", 30 }
            }
        };

        _mockAuditLogService
            .Setup(s => s.GetStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetStats(null, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var stats = okResult!.Value as AuditLogStatsDto;

        stats.Should().NotBeNull();
        stats!.TotalEntries.Should().Be(5000);
        stats.Last24Hours.Should().Be(100);
        stats.Last7Days.Should().Be(400);
        stats.Last30Days.Should().Be(1200);
        stats.ByCategory.Should().HaveCount(3);
        stats.ByAction.Should().HaveCount(3);
        stats.TopActors.Should().HaveCount(2);

        _mockAuditLogService.Verify(
            s => s.GetStatsAsync(null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStats_ShouldReturnGuildSpecificStats_WhenGuildIdProvided()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var expectedStats = new AuditLogStatsDto
        {
            TotalEntries = 1000,
            Last24Hours = 20,
            Last7Days = 80,
            Last30Days = 250,
            ByCategory = new Dictionary<AuditLogCategory, int>
            {
                { AuditLogCategory.Guild, 500 },
                { AuditLogCategory.User, 500 }
            }
        };

        _mockAuditLogService
            .Setup(s => s.GetStatsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetStats(guildId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var stats = okResult!.Value as AuditLogStatsDto;

        stats.Should().NotBeNull();
        stats!.TotalEntries.Should().Be(1000);
        stats.Last24Hours.Should().Be(20);

        _mockAuditLogService.Verify(
            s => s.GetStatsAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStats_ShouldReturnEmptyStats_WhenNoLogsExist()
    {
        // Arrange
        var expectedStats = new AuditLogStatsDto
        {
            TotalEntries = 0,
            Last24Hours = 0,
            Last7Days = 0,
            Last30Days = 0,
            ByCategory = new Dictionary<AuditLogCategory, int>(),
            ByAction = new Dictionary<AuditLogAction, int>(),
            TopActors = new Dictionary<string, int>()
        };

        _mockAuditLogService
            .Setup(s => s.GetStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _controller.GetStats(null, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var stats = okResult!.Value as AuditLogStatsDto;

        stats.Should().NotBeNull();
        stats!.TotalEntries.Should().Be(0);
        stats.Last24Hours.Should().Be(0);
        stats.ByCategory.Should().BeEmpty();
        stats.ByAction.Should().BeEmpty();
        stats.TopActors.Should().BeEmpty();
    }

    #endregion

    #region GetByCorrelationId Tests

    [Fact]
    public async Task GetByCorrelationId_ShouldReturnAllMatchingLogs()
    {
        // Arrange
        const string correlationId = "test-correlation-123";
        var logs = new List<AuditLogDto>
        {
            CreateTestAuditLogDto(1, correlationId: correlationId),
            CreateTestAuditLogDto(2, correlationId: correlationId),
            CreateTestAuditLogDto(3, correlationId: correlationId)
        };

        _mockAuditLogService
            .Setup(s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        // Act
        var result = await _controller.GetByCorrelationId(correlationId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedLogs = okResult!.Value as IEnumerable<AuditLogDto>;

        returnedLogs.Should().NotBeNull();
        returnedLogs.Should().HaveCount(3);
        returnedLogs.Should().OnlyContain(log => log.CorrelationId == correlationId);

        _mockAuditLogService.Verify(
            s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByCorrelationId_ShouldReturnEmptyList_WhenNoMatchingLogs()
    {
        // Arrange
        const string correlationId = "nonexistent-correlation";

        _mockAuditLogService
            .Setup(s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLogDto>());

        // Act
        var result = await _controller.GetByCorrelationId(correlationId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedLogs = okResult!.Value as IEnumerable<AuditLogDto>;

        returnedLogs.Should().NotBeNull();
        returnedLogs.Should().BeEmpty();

        _mockAuditLogService.Verify(
            s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByCorrelationId_ShouldReturnSingleLog_WhenOnlyOneMatch()
    {
        // Arrange
        const string correlationId = "single-correlation-456";
        var logs = new List<AuditLogDto>
        {
            CreateTestAuditLogDto(1, correlationId: correlationId)
        };

        _mockAuditLogService
            .Setup(s => s.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        // Act
        var result = await _controller.GetByCorrelationId(correlationId, CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var returnedLogs = okResult!.Value as IEnumerable<AuditLogDto>;

        returnedLogs.Should().NotBeNull();
        returnedLogs.Should().HaveCount(1);
        returnedLogs!.First().CorrelationId.Should().Be(correlationId);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test audit log DTO with default values.
    /// </summary>
    private static AuditLogDto CreateTestAuditLogDto(
        long id = 1,
        AuditLogCategory category = AuditLogCategory.Guild,
        AuditLogAction action = AuditLogAction.Updated,
        AuditLogActorType actorType = AuditLogActorType.User,
        string actorId = "user123",
        string? correlationId = null)
    {
        return new AuditLogDto
        {
            Id = id,
            Timestamp = DateTime.UtcNow,
            Category = category,
            CategoryName = category.ToString(),
            Action = action,
            ActionName = action.ToString(),
            ActorType = actorType,
            ActorTypeName = actorType.ToString(),
            ActorId = actorId,
            ActorDisplayName = "Test User",
            TargetType = "Guild",
            TargetId = "123456789",
            GuildId = 123456789UL,
            GuildName = "Test Guild",
            Details = "{\"name\":\"Test Guild\"}",
            IpAddress = "127.0.0.1",
            CorrelationId = correlationId
        };
    }

    #endregion
}
