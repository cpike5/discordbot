using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// Unit tests for AuditLogRepository.
/// </summary>
public class AuditLogRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly AuditLogRepository _repository;
    private readonly Mock<ILogger<AuditLogRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<AuditLog>>> _mockBaseLogger;

    public AuditLogRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<AuditLogRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<AuditLog>>>();
        _repository = new AuditLogRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    #region Helper Methods

    /// <summary>
    /// Creates a test audit log with sensible defaults.
    /// </summary>
    private static AuditLog CreateTestLog(
        AuditLogCategory category = AuditLogCategory.User,
        AuditLogAction action = AuditLogAction.Created,
        string actorId = "123456789",
        AuditLogActorType actorType = AuditLogActorType.User,
        string? targetType = "User",
        string? targetId = "987654321",
        ulong? guildId = 123456789,
        string? details = null,
        string? correlationId = null,
        DateTime? timestamp = null)
    {
        return new AuditLog
        {
            Timestamp = timestamp ?? DateTime.UtcNow,
            Category = category,
            Action = action,
            ActorId = actorId,
            ActorType = actorType,
            TargetType = targetType,
            TargetId = targetId,
            GuildId = guildId,
            Details = details,
            CorrelationId = correlationId
        };
    }

    #endregion

    #region CRUD Operations Tests

    public class CrudOperations : AuditLogRepositoryTests
    {
        [Fact]
        public async Task AddAsync_ShouldAddAuditLog_WhenValid()
        {
            // Arrange
            var log = CreateTestLog(
                category: AuditLogCategory.Security,
                action: AuditLogAction.PermissionChanged,
                details: "{\"permission\": \"Administrator\"}");

            // Act
            var result = await _repository.AddAsync(log);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            result.Category.Should().Be(AuditLogCategory.Security);
            result.Action.Should().Be(AuditLogAction.PermissionChanged);
            result.ActorId.Should().Be("123456789");
            result.ActorType.Should().Be(AuditLogActorType.User);
            result.TargetType.Should().Be("User");
            result.TargetId.Should().Be("987654321");
            result.GuildId.Should().Be(123456789);
            result.Details.Should().Be("{\"permission\": \"Administrator\"}");
            result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            // Verify it was saved to the database
            var savedLog = await _context.AuditLogs.FindAsync(result.Id);
            savedLog.Should().NotBeNull();
            savedLog!.Action.Should().Be(AuditLogAction.PermissionChanged);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnAuditLog_WhenExists()
        {
            // Arrange
            var log = CreateTestLog(action: AuditLogAction.Login);
            await _context.AuditLogs.AddAsync(log);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(log.Id);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(log.Id);
            result.Action.Should().Be(AuditLogAction.Login);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
        {
            // Arrange - Add a log to ensure the table exists
            var log = CreateTestLog();
            await _context.AuditLogs.AddAsync(log);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(999999L);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdateAuditLog()
        {
            // Arrange
            var log = CreateTestLog(details: "Original details");
            await _context.AuditLogs.AddAsync(log);
            await _context.SaveChangesAsync();

            // Act
            log.Details = "Updated details";
            await _repository.UpdateAsync(log);

            // Assert
            var updated = await _context.AuditLogs.FindAsync(log.Id);
            updated.Should().NotBeNull();
            updated!.Details.Should().Be("Updated details");
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveAuditLog()
        {
            // Arrange
            var log = CreateTestLog();
            await _context.AuditLogs.AddAsync(log);
            await _context.SaveChangesAsync();

            // Act
            await _repository.DeleteAsync(log);

            // Assert
            var deleted = await _context.AuditLogs.FindAsync(log.Id);
            deleted.Should().BeNull();
        }
    }

    #endregion

    #region GetLogsAsync Tests

    public class GetLogsAsyncTests : AuditLogRepositoryTests
    {
        [Fact]
        public async Task GetLogsAsync_ShouldReturnAllLogs_WhenNoFilters()
        {
            // Arrange
            var log1 = CreateTestLog(action: AuditLogAction.Created);
            var log2 = CreateTestLog(action: AuditLogAction.Updated);
            var log3 = CreateTestLog(action: AuditLogAction.Deleted);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { PageSize = 10 };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(3);
            totalCount.Should().Be(3);
        }

        [Fact]
        public async Task GetLogsAsync_ShouldFilterByCategory()
        {
            // Arrange
            var log1 = CreateTestLog(category: AuditLogCategory.User);
            var log2 = CreateTestLog(category: AuditLogCategory.Guild);
            var log3 = CreateTestLog(category: AuditLogCategory.Security);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { Category = AuditLogCategory.Security };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(1);
            totalCount.Should().Be(1);
            items[0].Category.Should().Be(AuditLogCategory.Security);
        }

        [Fact]
        public async Task GetLogsAsync_ShouldFilterByAction()
        {
            // Arrange
            var log1 = CreateTestLog(action: AuditLogAction.Created);
            var log2 = CreateTestLog(action: AuditLogAction.Updated);
            var log3 = CreateTestLog(action: AuditLogAction.Deleted);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { Action = AuditLogAction.Updated };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(1);
            totalCount.Should().Be(1);
            items[0].Action.Should().Be(AuditLogAction.Updated);
        }

        [Fact]
        public async Task GetLogsAsync_ShouldFilterByActorId()
        {
            // Arrange
            var log1 = CreateTestLog(actorId: "111111111");
            var log2 = CreateTestLog(actorId: "222222222");
            var log3 = CreateTestLog(actorId: "222222222");
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { ActorId = "222222222" };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(2);
            totalCount.Should().Be(2);
            items.Should().AllSatisfy(log => log.ActorId.Should().Be("222222222"));
        }

        [Fact]
        public async Task GetLogsAsync_ShouldFilterByActorType()
        {
            // Arrange
            var log1 = CreateTestLog(actorType: AuditLogActorType.User);
            var log2 = CreateTestLog(actorType: AuditLogActorType.System);
            var log3 = CreateTestLog(actorType: AuditLogActorType.Bot);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { ActorType = AuditLogActorType.System };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(1);
            totalCount.Should().Be(1);
            items[0].ActorType.Should().Be(AuditLogActorType.System);
        }

        [Fact]
        public async Task GetLogsAsync_ShouldFilterByGuildId()
        {
            // Arrange
            var log1 = CreateTestLog(guildId: 111111111);
            var log2 = CreateTestLog(guildId: 222222222);
            var log3 = CreateTestLog(guildId: 222222222);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { GuildId = 222222222 };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(2);
            totalCount.Should().Be(2);
            items.Should().AllSatisfy(log => log.GuildId.Should().Be(222222222));
        }

        [Fact]
        public async Task GetLogsAsync_ShouldFilterByDateRange()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var log1 = CreateTestLog(timestamp: now.AddDays(-10));
            var log2 = CreateTestLog(timestamp: now.AddDays(-5));
            var log3 = CreateTestLog(timestamp: now.AddDays(-2));
            var log4 = CreateTestLog(timestamp: now);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3, log4);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto
            {
                StartDate = now.AddDays(-6),
                EndDate = now.AddDays(-1)
            };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(2);
            totalCount.Should().Be(2);
            items.Should().Contain(log => log.Id == log2.Id);
            items.Should().Contain(log => log.Id == log3.Id);
        }

        [Fact]
        public async Task GetLogsAsync_ShouldFilterByCorrelationId()
        {
            // Arrange
            var log1 = CreateTestLog(correlationId: "corr-123");
            var log2 = CreateTestLog(correlationId: "corr-123");
            var log3 = CreateTestLog(correlationId: "corr-456");
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { CorrelationId = "corr-123" };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(2);
            totalCount.Should().Be(2);
            items.Should().AllSatisfy(log => log.CorrelationId.Should().Be("corr-123"));
        }

        [Fact]
        public async Task GetLogsAsync_ShouldFilterByTargetType()
        {
            // Arrange
            var log1 = CreateTestLog(targetType: "User");
            var log2 = CreateTestLog(targetType: "Guild");
            var log3 = CreateTestLog(targetType: "User");
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { TargetType = "Guild" };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(1);
            totalCount.Should().Be(1);
            items[0].TargetType.Should().Be("Guild");
        }

        [Fact]
        public async Task GetLogsAsync_ShouldFilterByTargetId()
        {
            // Arrange
            var log1 = CreateTestLog(targetId: "target-111");
            var log2 = CreateTestLog(targetId: "target-222");
            var log3 = CreateTestLog(targetId: "target-111");
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { TargetId = "target-111" };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(2);
            totalCount.Should().Be(2);
            items.Should().AllSatisfy(log => log.TargetId.Should().Be("target-111"));
        }

        [Fact]
        public async Task GetLogsAsync_ShouldSearchInDetails()
        {
            // Arrange
            var log1 = CreateTestLog(details: "User performed password reset");
            var log2 = CreateTestLog(details: "Guild settings updated");
            var log3 = CreateTestLog(details: "System password complexity increased");
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            // Note: SQLite Contains is case-sensitive, so we search for lowercase "password"
            var query = new AuditLogQueryDto { SearchTerm = "password" };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(2);
            totalCount.Should().Be(2);
            items.Should().Contain(log => log.Id == log1.Id);
            items.Should().Contain(log => log.Id == log3.Id);
        }

        [Fact]
        public async Task GetLogsAsync_ShouldApplyMultipleFilters()
        {
            // Arrange
            var log1 = CreateTestLog(
                category: AuditLogCategory.Security,
                action: AuditLogAction.PermissionChanged,
                guildId: 123456789,
                actorId: "user-123");

            var log2 = CreateTestLog(
                category: AuditLogCategory.Security,
                action: AuditLogAction.Created,
                guildId: 123456789,
                actorId: "user-123");

            var log3 = CreateTestLog(
                category: AuditLogCategory.Security,
                action: AuditLogAction.PermissionChanged,
                guildId: 999999999,
                actorId: "user-123");

            var log4 = CreateTestLog(
                category: AuditLogCategory.User,
                action: AuditLogAction.PermissionChanged,
                guildId: 123456789,
                actorId: "user-123");

            await _context.AuditLogs.AddRangeAsync(log1, log2, log3, log4);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto
            {
                Category = AuditLogCategory.Security,
                Action = AuditLogAction.PermissionChanged,
                GuildId = 123456789,
                ActorId = "user-123"
            };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(1);
            totalCount.Should().Be(1);
            items[0].Id.Should().Be(log1.Id);
        }

        [Fact]
        public async Task GetLogsAsync_ShouldReturnPaginatedResults()
        {
            // Arrange
            var now = DateTime.UtcNow;
            for (int i = 0; i < 25; i++)
            {
                var log = CreateTestLog(timestamp: now.AddMinutes(-i));
                await _context.AuditLogs.AddAsync(log);
            }
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { Page = 2, PageSize = 10 };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(10);
            totalCount.Should().Be(25);
        }

        [Fact]
        public async Task GetLogsAsync_ShouldReturnCorrectTotalCount()
        {
            // Arrange
            for (int i = 0; i < 15; i++)
            {
                var log = CreateTestLog(category: AuditLogCategory.User);
                await _context.AuditLogs.AddAsync(log);
            }

            for (int i = 0; i < 10; i++)
            {
                var log = CreateTestLog(category: AuditLogCategory.Guild);
                await _context.AuditLogs.AddAsync(log);
            }
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto
            {
                Category = AuditLogCategory.User,
                PageSize = 5
            };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(5);
            totalCount.Should().Be(15, "total count should reflect all matching items, not just the page");
        }

        [Fact]
        public async Task GetLogsAsync_ShouldOrderByTimestampDescending()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var log1 = CreateTestLog(timestamp: now.AddHours(-3));
            var log2 = CreateTestLog(timestamp: now.AddHours(-1));
            var log3 = CreateTestLog(timestamp: now.AddHours(-2));
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { PageSize = 10 };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(3);
            items[0].Id.Should().Be(log2.Id, "most recent should be first");
            items[1].Id.Should().Be(log3.Id);
            items[2].Id.Should().Be(log1.Id, "oldest should be last");
        }

        [Fact]
        public async Task GetLogsAsync_ShouldSortByCategory_WhenSpecified()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var log1 = CreateTestLog(category: AuditLogCategory.User, timestamp: now.AddMinutes(-3));
            var log2 = CreateTestLog(category: AuditLogCategory.Security, timestamp: now.AddMinutes(-2));
            var log3 = CreateTestLog(category: AuditLogCategory.Guild, timestamp: now.AddMinutes(-1));
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { SortBy = "category", SortDescending = false };

            // Act
            var (items, _) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(3);
            // Categories are enums with values: User=1, Guild=2, Security=4
            items[0].Category.Should().Be(AuditLogCategory.User);
            items[1].Category.Should().Be(AuditLogCategory.Guild);
            items[2].Category.Should().Be(AuditLogCategory.Security);
        }

        [Fact]
        public async Task GetLogsAsync_ShouldSortByAction_WhenSpecified()
        {
            // Arrange
            var log1 = CreateTestLog(action: AuditLogAction.Deleted);
            var log2 = CreateTestLog(action: AuditLogAction.Created);
            var log3 = CreateTestLog(action: AuditLogAction.Updated);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { SortBy = "action", SortDescending = false };

            // Act
            var (items, _) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(3);
            items[0].Action.Should().Be(AuditLogAction.Created);
            items[1].Action.Should().Be(AuditLogAction.Updated);
            items[2].Action.Should().Be(AuditLogAction.Deleted);
        }
    }

    #endregion

    #region GetByCorrelationIdAsync Tests

    public class GetByCorrelationIdAsyncTests : AuditLogRepositoryTests
    {
        [Fact]
        public async Task GetByCorrelationIdAsync_ShouldReturnRelatedLogs()
        {
            // Arrange
            var correlationId = "operation-12345";
            var now = DateTime.UtcNow;

            var log1 = CreateTestLog(
                correlationId: correlationId,
                action: AuditLogAction.Created,
                timestamp: now.AddSeconds(1));

            var log2 = CreateTestLog(
                correlationId: correlationId,
                action: AuditLogAction.Updated,
                timestamp: now.AddSeconds(2));

            var log3 = CreateTestLog(
                correlationId: correlationId,
                action: AuditLogAction.Deleted,
                timestamp: now.AddSeconds(3));

            var log4 = CreateTestLog(
                correlationId: "other-operation",
                action: AuditLogAction.Created);

            await _context.AuditLogs.AddRangeAsync(log1, log2, log3, log4);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByCorrelationIdAsync(correlationId);

            // Assert
            result.Should().HaveCount(3);
            result.Should().AllSatisfy(log => log.CorrelationId.Should().Be(correlationId));
            result[0].Id.Should().Be(log1.Id, "results should be ordered by timestamp ascending");
            result[1].Id.Should().Be(log2.Id);
            result[2].Id.Should().Be(log3.Id);
        }

        [Fact]
        public async Task GetByCorrelationIdAsync_ShouldReturnEmpty_WhenNotFound()
        {
            // Arrange
            var log = CreateTestLog(correlationId: "existing-correlation");
            await _context.AuditLogs.AddAsync(log);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByCorrelationIdAsync("non-existent-correlation");

            // Assert
            result.Should().BeEmpty();
        }
    }

    #endregion

    #region GetRecentByActorAsync Tests

    public class GetRecentByActorAsyncTests : AuditLogRepositoryTests
    {
        [Fact]
        public async Task GetRecentByActorAsync_ShouldReturnLogsForActor()
        {
            // Arrange
            var actorId = "user-123";
            var log1 = CreateTestLog(actorId: actorId, action: AuditLogAction.Login);
            var log2 = CreateTestLog(actorId: actorId, action: AuditLogAction.Updated);
            var log3 = CreateTestLog(actorId: "user-456", action: AuditLogAction.Created);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetRecentByActorAsync(actorId);

            // Assert
            result.Should().HaveCount(2);
            result.Should().AllSatisfy(log => log.ActorId.Should().Be(actorId));
        }

        [Fact]
        public async Task GetRecentByActorAsync_ShouldRespectLimit()
        {
            // Arrange
            var actorId = "user-123";
            for (int i = 0; i < 100; i++)
            {
                var log = CreateTestLog(actorId: actorId);
                await _context.AuditLogs.AddAsync(log);
            }
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetRecentByActorAsync(actorId, limit: 25);

            // Assert
            result.Should().HaveCount(25);
        }

        [Fact]
        public async Task GetRecentByActorAsync_ShouldOrderByTimestampDescending()
        {
            // Arrange
            var actorId = "user-123";
            var now = DateTime.UtcNow;
            var log1 = CreateTestLog(actorId: actorId, timestamp: now.AddHours(-3));
            var log2 = CreateTestLog(actorId: actorId, timestamp: now.AddHours(-1));
            var log3 = CreateTestLog(actorId: actorId, timestamp: now.AddHours(-2));
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetRecentByActorAsync(actorId);

            // Assert
            result.Should().HaveCount(3);
            result[0].Id.Should().Be(log2.Id, "most recent should be first");
            result[1].Id.Should().Be(log3.Id);
            result[2].Id.Should().Be(log1.Id);
        }
    }

    #endregion

    #region BulkInsertAsync Tests

    public class BulkInsertAsyncTests : AuditLogRepositoryTests
    {
        [Fact]
        public async Task BulkInsertAsync_ShouldInsertMultipleLogs()
        {
            // Arrange
            var logs = new List<AuditLog>
            {
                CreateTestLog(action: AuditLogAction.Created),
                CreateTestLog(action: AuditLogAction.Updated),
                CreateTestLog(action: AuditLogAction.Deleted)
            };

            // Act
            await _repository.BulkInsertAsync(logs);

            // Assert
            var allLogs = _context.AuditLogs.ToList();
            allLogs.Should().HaveCount(3);
            allLogs.Should().Contain(log => log.Action == AuditLogAction.Created);
            allLogs.Should().Contain(log => log.Action == AuditLogAction.Updated);
            allLogs.Should().Contain(log => log.Action == AuditLogAction.Deleted);
        }

        [Fact]
        public async Task BulkInsertAsync_ShouldHandleEmptyList()
        {
            // Arrange
            var logs = new List<AuditLog>();

            // Act
            await _repository.BulkInsertAsync(logs);

            // Assert
            var allLogs = _context.AuditLogs.ToList();
            allLogs.Should().BeEmpty();
        }

        [Fact]
        public async Task BulkInsertAsync_ShouldAssignIds()
        {
            // Arrange
            var logs = new List<AuditLog>
            {
                CreateTestLog(action: AuditLogAction.Created),
                CreateTestLog(action: AuditLogAction.Updated)
            };

            // Act
            await _repository.BulkInsertAsync(logs);

            // Assert
            logs.Should().AllSatisfy(log => log.Id.Should().BeGreaterThan(0));
        }
    }

    #endregion

    #region GetStatsAsync Tests

    public class GetStatsAsyncTests : AuditLogRepositoryTests
    {
        [Fact]
        public async Task GetStatsAsync_ShouldReturnCorrectTotalCount()
        {
            // Arrange
            for (int i = 0; i < 10; i++)
            {
                var log = CreateTestLog();
                await _context.AuditLogs.AddAsync(log);
            }
            await _context.SaveChangesAsync();

            // Act
            var stats = await _repository.GetStatsAsync();

            // Assert
            stats.TotalEntries.Should().Be(10);
        }

        [Fact]
        public async Task GetStatsAsync_ShouldCalculateLast24HoursCount()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var log1 = CreateTestLog(timestamp: now.AddHours(-2));
            var log2 = CreateTestLog(timestamp: now.AddHours(-12));
            var log3 = CreateTestLog(timestamp: now.AddHours(-30)); // Outside 24 hours
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            // Act
            var stats = await _repository.GetStatsAsync();

            // Assert
            stats.Last24Hours.Should().Be(2);
        }

        [Fact]
        public async Task GetStatsAsync_ShouldCalculateLast7DaysCount()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var log1 = CreateTestLog(timestamp: now.AddDays(-1));
            var log2 = CreateTestLog(timestamp: now.AddDays(-5));
            var log3 = CreateTestLog(timestamp: now.AddDays(-10)); // Outside 7 days
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            // Act
            var stats = await _repository.GetStatsAsync();

            // Assert
            stats.Last7Days.Should().Be(2);
        }

        [Fact]
        public async Task GetStatsAsync_ShouldCalculateLast30DaysCount()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var log1 = CreateTestLog(timestamp: now.AddDays(-5));
            var log2 = CreateTestLog(timestamp: now.AddDays(-15));
            var log3 = CreateTestLog(timestamp: now.AddDays(-25));
            var log4 = CreateTestLog(timestamp: now.AddDays(-40)); // Outside 30 days
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3, log4);
            await _context.SaveChangesAsync();

            // Act
            var stats = await _repository.GetStatsAsync();

            // Assert
            stats.Last30Days.Should().Be(3);
        }

        [Fact]
        public async Task GetStatsAsync_ShouldCalculateCategoryBreakdown()
        {
            // Arrange
            var log1 = CreateTestLog(category: AuditLogCategory.User);
            var log2 = CreateTestLog(category: AuditLogCategory.User);
            var log3 = CreateTestLog(category: AuditLogCategory.Security);
            var log4 = CreateTestLog(category: AuditLogCategory.Guild);
            var log5 = CreateTestLog(category: AuditLogCategory.Guild);
            var log6 = CreateTestLog(category: AuditLogCategory.Guild);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3, log4, log5, log6);
            await _context.SaveChangesAsync();

            // Act
            var stats = await _repository.GetStatsAsync();

            // Assert
            stats.ByCategory.Should().HaveCount(3);
            stats.ByCategory[AuditLogCategory.User].Should().Be(2);
            stats.ByCategory[AuditLogCategory.Security].Should().Be(1);
            stats.ByCategory[AuditLogCategory.Guild].Should().Be(3);
        }

        [Fact]
        public async Task GetStatsAsync_ShouldCalculateActionBreakdown()
        {
            // Arrange
            var log1 = CreateTestLog(action: AuditLogAction.Created);
            var log2 = CreateTestLog(action: AuditLogAction.Updated);
            var log3 = CreateTestLog(action: AuditLogAction.Updated);
            var log4 = CreateTestLog(action: AuditLogAction.Deleted);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3, log4);
            await _context.SaveChangesAsync();

            // Act
            var stats = await _repository.GetStatsAsync();

            // Assert
            stats.ByAction.Should().HaveCount(3);
            stats.ByAction[AuditLogAction.Created].Should().Be(1);
            stats.ByAction[AuditLogAction.Updated].Should().Be(2);
            stats.ByAction[AuditLogAction.Deleted].Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_ShouldCalculateActorTypeBreakdown()
        {
            // Arrange
            var log1 = CreateTestLog(actorType: AuditLogActorType.User);
            var log2 = CreateTestLog(actorType: AuditLogActorType.User);
            var log3 = CreateTestLog(actorType: AuditLogActorType.System);
            var log4 = CreateTestLog(actorType: AuditLogActorType.Bot);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3, log4);
            await _context.SaveChangesAsync();

            // Act
            var stats = await _repository.GetStatsAsync();

            // Assert
            stats.ByActorType.Should().HaveCount(3);
            stats.ByActorType[AuditLogActorType.User].Should().Be(2);
            stats.ByActorType[AuditLogActorType.System].Should().Be(1);
            stats.ByActorType[AuditLogActorType.Bot].Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_ShouldCalculateTopActors()
        {
            // Arrange
            var log1 = CreateTestLog(actorId: "user-123");
            var log2 = CreateTestLog(actorId: "user-123");
            var log3 = CreateTestLog(actorId: "user-123");
            var log4 = CreateTestLog(actorId: "user-456");
            var log5 = CreateTestLog(actorId: "user-456");
            var log6 = CreateTestLog(actorId: "user-789");
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3, log4, log5, log6);
            await _context.SaveChangesAsync();

            // Act
            var stats = await _repository.GetStatsAsync();

            // Assert
            stats.TopActors.Should().HaveCount(3);
            stats.TopActors["user-123"].Should().Be(3);
            stats.TopActors["user-456"].Should().Be(2);
            stats.TopActors["user-789"].Should().Be(1);
        }

        [Fact]
        public async Task GetStatsAsync_ShouldLimitTopActorsToTen()
        {
            // Arrange
            for (int i = 1; i <= 15; i++)
            {
                var log = CreateTestLog(actorId: $"user-{i}");
                await _context.AuditLogs.AddAsync(log);
            }
            await _context.SaveChangesAsync();

            // Act
            var stats = await _repository.GetStatsAsync();

            // Assert
            stats.TopActors.Should().HaveCount(10);
        }

        [Fact]
        public async Task GetStatsAsync_ShouldFilterByGuild_WhenProvided()
        {
            // Arrange
            var log1 = CreateTestLog(guildId: 123456789, category: AuditLogCategory.User);
            var log2 = CreateTestLog(guildId: 123456789, category: AuditLogCategory.Guild);
            var log3 = CreateTestLog(guildId: 999999999, category: AuditLogCategory.Security);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            // Act
            var stats = await _repository.GetStatsAsync(guildId: 123456789);

            // Assert
            stats.TotalEntries.Should().Be(2);
            stats.ByCategory.Should().HaveCount(2);
            stats.ByCategory.Should().ContainKey(AuditLogCategory.User);
            stats.ByCategory.Should().ContainKey(AuditLogCategory.Guild);
            stats.ByCategory.Should().NotContainKey(AuditLogCategory.Security);
        }

        [Fact]
        public async Task GetStatsAsync_ShouldSetOldestAndNewestEntry()
        {
            // Arrange
            var now = DateTime.UtcNow;
            var log1 = CreateTestLog(timestamp: now.AddDays(-10));
            var log2 = CreateTestLog(timestamp: now.AddDays(-5));
            var log3 = CreateTestLog(timestamp: now);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            // Act
            var stats = await _repository.GetStatsAsync();

            // Assert
            stats.OldestEntry.Should().NotBeNull();
            stats.OldestEntry.Should().BeCloseTo(now.AddDays(-10), TimeSpan.FromSeconds(1));
            stats.NewestEntry.Should().NotBeNull();
            stats.NewestEntry.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task GetStatsAsync_ShouldReturnZeroStats_WhenNoLogs()
        {
            // Act
            var stats = await _repository.GetStatsAsync();

            // Assert
            stats.TotalEntries.Should().Be(0);
            stats.Last24Hours.Should().Be(0);
            stats.Last7Days.Should().Be(0);
            stats.Last30Days.Should().Be(0);
            stats.ByCategory.Should().BeEmpty();
            stats.ByAction.Should().BeEmpty();
            stats.ByActorType.Should().BeEmpty();
            stats.TopActors.Should().BeEmpty();
            // When no logs exist, FirstOrDefaultAsync on Select(l => l.Timestamp) returns default(DateTime)
            // which is DateTime.MinValue, and this gets implicitly converted to DateTime?
            stats.OldestEntry.Should().Be(DateTime.MinValue);
            stats.NewestEntry.Should().Be(DateTime.MinValue);
        }
    }

    #endregion

    #region DeleteOlderThanAsync Tests

    public class DeleteOlderThanAsyncTests : AuditLogRepositoryTests
    {
        [Fact]
        public async Task DeleteOlderThanAsync_ShouldDeleteOldLogs()
        {
            // Arrange
            var cutoffDate = DateTime.UtcNow.AddDays(-7);
            var log1 = CreateTestLog(timestamp: cutoffDate.AddDays(-10));
            var log2 = CreateTestLog(timestamp: cutoffDate.AddDays(-5));
            var log3 = CreateTestLog(timestamp: cutoffDate.AddDays(1));
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            // Act
            var deletedCount = await _repository.DeleteOlderThanAsync(cutoffDate);

            // Assert
            deletedCount.Should().Be(2);
            var remainingLogs = _context.AuditLogs.ToList();
            remainingLogs.Should().HaveCount(1);
            remainingLogs[0].Id.Should().Be(log3.Id);
        }

        [Fact]
        public async Task DeleteOlderThanAsync_ShouldReturnDeletedCount()
        {
            // Arrange
            var cutoffDate = DateTime.UtcNow.AddDays(-30);
            for (int i = 0; i < 15; i++)
            {
                var log = CreateTestLog(timestamp: cutoffDate.AddDays(-i - 1));
                await _context.AuditLogs.AddAsync(log);
            }
            await _context.SaveChangesAsync();

            // Act
            var deletedCount = await _repository.DeleteOlderThanAsync(cutoffDate);

            // Assert
            deletedCount.Should().Be(15);
        }

        [Fact]
        public async Task DeleteOlderThanAsync_ShouldNotDeleteNewerLogs()
        {
            // Arrange
            var cutoffDate = DateTime.UtcNow.AddDays(-7);
            var log1 = CreateTestLog(timestamp: cutoffDate.AddHours(1));
            var log2 = CreateTestLog(timestamp: cutoffDate.AddDays(1));
            var log3 = CreateTestLog(timestamp: DateTime.UtcNow);
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            // Act
            var deletedCount = await _repository.DeleteOlderThanAsync(cutoffDate);

            // Assert
            deletedCount.Should().Be(0);
            var remainingLogs = _context.AuditLogs.ToList();
            remainingLogs.Should().HaveCount(3);
        }

        [Fact]
        public async Task DeleteOlderThanAsync_ShouldReturnZero_WhenNoLogsToDelete()
        {
            // Arrange
            var log = CreateTestLog(timestamp: DateTime.UtcNow);
            await _context.AuditLogs.AddAsync(log);
            await _context.SaveChangesAsync();

            // Act
            var deletedCount = await _repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-30));

            // Assert
            deletedCount.Should().Be(0);
        }

        [Fact]
        public async Task DeleteOlderThanAsync_ShouldHandleExactCutoffDate()
        {
            // Arrange
            var cutoffDate = DateTime.UtcNow.AddDays(-7);
            var log1 = CreateTestLog(timestamp: cutoffDate.AddSeconds(-1)); // Before cutoff
            var log2 = CreateTestLog(timestamp: cutoffDate); // Exactly at cutoff
            var log3 = CreateTestLog(timestamp: cutoffDate.AddSeconds(1)); // After cutoff
            await _context.AuditLogs.AddRangeAsync(log1, log2, log3);
            await _context.SaveChangesAsync();

            // Act
            var deletedCount = await _repository.DeleteOlderThanAsync(cutoffDate);

            // Assert
            deletedCount.Should().Be(1, "only logs strictly before cutoff should be deleted");
            var remainingLogs = _context.AuditLogs.OrderBy(l => l.Timestamp).ToList();
            remainingLogs.Should().HaveCount(2);
            remainingLogs[0].Id.Should().Be(log2.Id);
            remainingLogs[1].Id.Should().Be(log3.Id);
        }
    }

    #endregion

    #region Edge Cases and Additional Tests

    public class EdgeCaseTests : AuditLogRepositoryTests
    {
        [Fact]
        public async Task GetLogsAsync_ShouldHandleNullGuildIdFilter()
        {
            // Arrange
            var log1 = CreateTestLog(guildId: 123456789);
            var log2 = CreateTestLog(guildId: null);
            await _context.AuditLogs.AddRangeAsync(log1, log2);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { GuildId = null };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(2, "null GuildId filter should return all logs");
        }

        [Fact]
        public async Task GetLogsAsync_ShouldHandleEmptySearchTerm()
        {
            // Arrange
            var log1 = CreateTestLog(details: "Some details");
            var log2 = CreateTestLog(details: "Other details");
            await _context.AuditLogs.AddRangeAsync(log1, log2);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { SearchTerm = "" };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(2, "empty search term should return all logs");
        }

        [Fact]
        public async Task GetLogsAsync_ShouldHandleWhitespaceSearchTerm()
        {
            // Arrange
            var log1 = CreateTestLog(details: "Some details");
            var log2 = CreateTestLog(details: "Other details");
            await _context.AuditLogs.AddRangeAsync(log1, log2);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { SearchTerm = "   " };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(2, "whitespace search term should return all logs");
        }

        [Fact]
        public async Task GetLogsAsync_ShouldHandleNullDetails()
        {
            // Arrange
            var log1 = CreateTestLog(details: "Some details");
            var log2 = CreateTestLog(details: null);
            await _context.AuditLogs.AddRangeAsync(log1, log2);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { SearchTerm = "details" };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().HaveCount(1);
            items[0].Id.Should().Be(log1.Id);
        }

        [Fact]
        public async Task GetRecentByActorAsync_ShouldReturnEmpty_WhenActorNotFound()
        {
            // Arrange
            var log = CreateTestLog(actorId: "user-123");
            await _context.AuditLogs.AddAsync(log);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetRecentByActorAsync("user-999");

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetStatsAsync_ShouldHandleNullActorIds()
        {
            // Arrange
            var log1 = CreateTestLog(actorId: "user-123");
            var log2 = CreateTestLog(actorId: null);
            await _context.AuditLogs.AddRangeAsync(log1, log2);
            await _context.SaveChangesAsync();

            // Act
            var stats = await _repository.GetStatsAsync();

            // Assert
            stats.TopActors.Should().HaveCount(1);
            stats.TopActors.Should().ContainKey("user-123");
        }

        [Fact]
        public async Task GetLogsAsync_ShouldHandlePageBeyondResults()
        {
            // Arrange
            var log = CreateTestLog();
            await _context.AuditLogs.AddAsync(log);
            await _context.SaveChangesAsync();

            var query = new AuditLogQueryDto { Page = 5, PageSize = 10 };

            // Act
            var (items, totalCount) = await _repository.GetLogsAsync(query);

            // Assert
            items.Should().BeEmpty();
            totalCount.Should().Be(1);
        }
    }

    #endregion
}
