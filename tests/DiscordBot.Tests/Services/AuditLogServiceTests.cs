using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AuditLogService"/>.
/// Tests cover querying, retrieval, logging, and builder creation.
/// </summary>
[Trait("Category", "Unit")]
public class AuditLogServiceTests
{
    private readonly Mock<IAuditLogRepository> _mockRepository;
    private readonly Mock<IAuditLogQueue> _mockQueue;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<DiscordSocketClient> _mockDiscordClient;
    private readonly Mock<ILogger<AuditLogService>> _mockLogger;
    private readonly AuditLogService _service;

    public AuditLogServiceTests()
    {
        _mockRepository = new Mock<IAuditLogRepository>();
        _mockQueue = new Mock<IAuditLogQueue>();
        _mockUserManager = MockUserManager();
        _mockDiscordClient = new Mock<DiscordSocketClient>();
        _mockLogger = new Mock<ILogger<AuditLogService>>();

        _service = new AuditLogService(
            _mockRepository.Object,
            _mockQueue.Object,
            _mockUserManager.Object,
            _mockDiscordClient.Object,
            _mockLogger.Object);
    }

    private static Mock<UserManager<ApplicationUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    #region GetLogsAsync Tests

    [Fact]
    public async Task GetLogsAsync_ReturnsEmptyList_WhenNoLogs()
    {
        // Arrange
        var query = new AuditLogQueryDto { Page = 1, PageSize = 20 };
        _mockRepository
            .Setup(r => r.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLog>(), 0));

        // Act
        var (items, totalCount) = await _service.GetLogsAsync(query);

        // Assert
        items.Should().BeEmpty();
        totalCount.Should().Be(0);

        _mockRepository.Verify(
            r => r.GetLogsAsync(query, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLogsAsync_AppliesPaginationCorrectly()
    {
        // Arrange
        var query = new AuditLogQueryDto { Page = 2, PageSize = 5 };
        var logs = new List<AuditLog>
        {
            CreateTestAuditLog(1),
            CreateTestAuditLog(2),
            CreateTestAuditLog(3)
        };

        _mockRepository
            .Setup(r => r.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((logs, 15));

        // Act
        var (items, totalCount) = await _service.GetLogsAsync(query);

        // Assert
        items.Should().HaveCount(3);
        totalCount.Should().Be(15);
        items.Should().OnlyContain(dto => dto.Category == AuditLogCategory.Guild);

        _mockRepository.Verify(
            r => r.GetLogsAsync(query, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetLogsAsync_ValidatesAndNormalizesPageNumber()
    {
        // Arrange
        var query = new AuditLogQueryDto { Page = 0, PageSize = 20 }; // Invalid page
        _mockRepository
            .Setup(r => r.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLog>(), 0));

        // Act
        await _service.GetLogsAsync(query);

        // Assert
        query.Page.Should().Be(1, "page number should be normalized to minimum value of 1");
    }

    [Fact]
    public async Task GetLogsAsync_ValidatesAndNormalizesPageSize()
    {
        // Arrange
        var query = new AuditLogQueryDto { Page = 1, PageSize = 200 }; // Too large
        _mockRepository
            .Setup(r => r.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLog>(), 0));

        // Act
        await _service.GetLogsAsync(query);

        // Assert
        query.PageSize.Should().Be(20, "page size should be normalized to default when exceeding maximum");
    }

    [Fact]
    public async Task GetLogsAsync_ValidatesAndNormalizesNegativePageSize()
    {
        // Arrange
        var query = new AuditLogQueryDto { Page = 1, PageSize = -5 }; // Invalid
        _mockRepository
            .Setup(r => r.GetLogsAsync(It.IsAny<AuditLogQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<AuditLog>(), 0));

        // Act
        await _service.GetLogsAsync(query);

        // Assert
        query.PageSize.Should().Be(20, "page size should be normalized to default when invalid");
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        // Arrange
        const long logId = 999;
        _mockRepository
            .Setup(r => r.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLog?)null);

        // Act
        var result = await _service.GetByIdAsync(logId);

        // Assert
        result.Should().BeNull();

        _mockRepository.Verify(
            r => r.GetByIdAsync(logId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsMappedDto_WhenFound()
    {
        // Arrange
        const long logId = 123;
        var log = CreateTestAuditLog(logId);

        _mockRepository
            .Setup(r => r.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);

        // Act
        var result = await _service.GetByIdAsync(logId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(logId);
        result.Category.Should().Be(AuditLogCategory.Guild);
        result.CategoryName.Should().Be("Guild");
        result.Action.Should().Be(AuditLogAction.Updated);
        result.ActionName.Should().Be("Updated");
        result.ActorType.Should().Be(AuditLogActorType.User);
        result.ActorTypeName.Should().Be("User");
        result.ActorId.Should().Be("user123");
    }

    #endregion

    #region GetByCorrelationIdAsync Tests

    [Fact]
    public async Task GetByCorrelationIdAsync_ReturnsRelatedEntries()
    {
        // Arrange
        const string correlationId = "test-correlation-123";
        var logs = new List<AuditLog>
        {
            CreateTestAuditLog(1, correlationId: correlationId),
            CreateTestAuditLog(2, correlationId: correlationId),
            CreateTestAuditLog(3, correlationId: correlationId)
        };

        _mockRepository
            .Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        // Act
        var result = await _service.GetByCorrelationIdAsync(correlationId);

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyContain(dto => dto.CorrelationId == correlationId);

        _mockRepository.Verify(
            r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByCorrelationIdAsync_ReturnsEmpty_WhenNoMatches()
    {
        // Arrange
        const string correlationId = "nonexistent-correlation";
        _mockRepository
            .Setup(r => r.GetByCorrelationIdAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLog>());

        // Act
        var result = await _service.GetByCorrelationIdAsync(correlationId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetStatsAsync Tests

    [Fact]
    public async Task GetStatsAsync_DelegatesToRepository()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var expectedStats = new AuditLogStatsDto
        {
            TotalEntries = 1000,
            Last24Hours = 50,
            Last7Days = 200,
            Last30Days = 500
        };

        _mockRepository
            .Setup(r => r.GetStatsAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _service.GetStatsAsync(guildId);

        // Assert
        result.Should().NotBeNull();
        result.TotalEntries.Should().Be(1000);
        result.Last24Hours.Should().Be(50);
        result.Last7Days.Should().Be(200);
        result.Last30Days.Should().Be(500);

        _mockRepository.Verify(
            r => r.GetStatsAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStatsAsync_WithNullGuildId_ReturnsGlobalStats()
    {
        // Arrange
        var expectedStats = new AuditLogStatsDto
        {
            TotalEntries = 5000,
            Last24Hours = 100,
            Last7Days = 400,
            Last30Days = 1200
        };

        _mockRepository
            .Setup(r => r.GetStatsAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStats);

        // Act
        var result = await _service.GetStatsAsync(null);

        // Assert
        result.Should().NotBeNull();
        result.TotalEntries.Should().Be(5000);
    }

    #endregion

    #region LogAsync Tests

    [Fact]
    public async Task LogAsync_EnqueuesLogEntry()
    {
        // Arrange
        var logDto = new AuditLogCreateDto
        {
            Category = AuditLogCategory.User,
            Action = AuditLogAction.Created,
            ActorType = AuditLogActorType.User,
            ActorId = "admin123"
        };

        // Act
        await _service.LogAsync(logDto);

        // Assert
        _mockQueue.Verify(
            q => q.Enqueue(logDto),
            Times.Once,
            "log entry should be enqueued for background processing");
    }

    [Fact]
    public async Task LogAsync_CompletesImmediately_FireAndForget()
    {
        // Arrange
        var logDto = new AuditLogCreateDto
        {
            Category = AuditLogCategory.System,
            Action = AuditLogAction.CommandExecuted,
            ActorType = AuditLogActorType.System,
            ActorId = "system"
        };

        // Act
        var task = _service.LogAsync(logDto);

        // Assert
        task.IsCompleted.Should().BeTrue("LogAsync should be a fire-and-forget operation");
    }

    #endregion

    #region CreateBuilder Tests

    [Fact]
    public void CreateBuilder_ReturnsValidBuilder()
    {
        // Act
        var builder = _service.CreateBuilder();

        // Assert
        builder.Should().NotBeNull();
        builder.Should().BeOfType<AuditLogBuilder>();
    }

    [Fact]
    public void CreateBuilder_ReturnsNewBuilderEachTime()
    {
        // Act
        var builder1 = _service.CreateBuilder();
        var builder2 = _service.CreateBuilder();

        // Assert
        builder1.Should().NotBeSameAs(builder2, "each call should return a new builder instance");
    }

    #endregion

    #region Helper Methods

    private static AuditLog CreateTestAuditLog(
        long id = 1,
        AuditLogCategory category = AuditLogCategory.Guild,
        AuditLogAction action = AuditLogAction.Updated,
        AuditLogActorType actorType = AuditLogActorType.User,
        string actorId = "user123",
        string? correlationId = null)
    {
        return new AuditLog
        {
            Id = id,
            Timestamp = DateTime.UtcNow,
            Category = category,
            Action = action,
            ActorType = actorType,
            ActorId = actorId,
            TargetType = "Guild",
            TargetId = "123456789",
            GuildId = 123456789UL,
            Details = "{\"name\":\"Test Guild\"}",
            IpAddress = "127.0.0.1",
            CorrelationId = correlationId
        };
    }

    #endregion
}
