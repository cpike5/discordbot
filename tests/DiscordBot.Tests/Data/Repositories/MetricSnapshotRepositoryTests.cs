using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// Unit tests for MetricSnapshotRepository.
/// </summary>
public class MetricSnapshotRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly MetricSnapshotRepository _repository;
    private readonly Mock<ILogger<MetricSnapshotRepository>> _mockLogger;

    public MetricSnapshotRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<MetricSnapshotRepository>>();
        _repository = new MetricSnapshotRepository(_context, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    #region Helper Methods

    /// <summary>
    /// Creates a test metric snapshot with sensible defaults.
    /// </summary>
    private static MetricSnapshot CreateTestSnapshot(
        DateTime? timestamp = null,
        double databaseAvgQueryTimeMs = 10.5,
        long workingSetMB = 256,
        long heapSizeMB = 128,
        double cacheHitRatePercent = 85.5,
        int servicesRunningCount = 5,
        int gen0Collections = 100,
        int gen1Collections = 50,
        int gen2Collections = 10)
    {
        return new MetricSnapshot
        {
            Timestamp = timestamp ?? DateTime.UtcNow,
            DatabaseAvgQueryTimeMs = databaseAvgQueryTimeMs,
            DatabaseTotalQueries = 1000,
            DatabaseSlowQueryCount = 5,
            WorkingSetMB = workingSetMB,
            PrivateMemoryMB = 200,
            HeapSizeMB = heapSizeMB,
            Gen0Collections = gen0Collections,
            Gen1Collections = gen1Collections,
            Gen2Collections = gen2Collections,
            CacheHitRatePercent = cacheHitRatePercent,
            CacheTotalEntries = 100,
            CacheTotalHits = 850,
            CacheTotalMisses = 150,
            ServicesRunningCount = servicesRunningCount,
            ServicesErrorCount = 0,
            ServicesTotalCount = 5
        };
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_ShouldAddSnapshot_WhenValid()
    {
        // Arrange
        var snapshot = CreateTestSnapshot(
            timestamp: new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            databaseAvgQueryTimeMs: 15.75,
            workingSetMB: 512);

        // Act
        await _repository.AddAsync(snapshot);

        // Assert
        snapshot.Id.Should().BeGreaterThan(0);

        // Verify it was persisted
        var saved = await _context.MetricSnapshots.FindAsync(snapshot.Id);
        saved.Should().NotBeNull();
        saved!.Timestamp.Should().Be(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc));
        saved.DatabaseAvgQueryTimeMs.Should().Be(15.75);
        saved.WorkingSetMB.Should().Be(512);
    }

    [Fact]
    public async Task AddAsync_ShouldPersistAllFields_WhenCalled()
    {
        // Arrange
        var snapshot = new MetricSnapshot
        {
            Timestamp = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
            DatabaseAvgQueryTimeMs = 20.5,
            DatabaseTotalQueries = 5000,
            DatabaseSlowQueryCount = 10,
            WorkingSetMB = 384,
            PrivateMemoryMB = 300,
            HeapSizeMB = 256,
            Gen0Collections = 200,
            Gen1Collections = 100,
            Gen2Collections = 25,
            CacheHitRatePercent = 92.5,
            CacheTotalEntries = 500,
            CacheTotalHits = 4625,
            CacheTotalMisses = 375,
            ServicesRunningCount = 8,
            ServicesErrorCount = 1,
            ServicesTotalCount = 9
        };

        // Act
        await _repository.AddAsync(snapshot);

        // Assert
        var saved = await _context.MetricSnapshots.FindAsync(snapshot.Id);
        saved.Should().NotBeNull();
        saved!.DatabaseTotalQueries.Should().Be(5000);
        saved.DatabaseSlowQueryCount.Should().Be(10);
        saved.PrivateMemoryMB.Should().Be(300);
        saved.HeapSizeMB.Should().Be(256);
        saved.Gen0Collections.Should().Be(200);
        saved.Gen1Collections.Should().Be(100);
        saved.Gen2Collections.Should().Be(25);
        saved.CacheTotalEntries.Should().Be(500);
        saved.ServicesErrorCount.Should().Be(1);
        saved.ServicesTotalCount.Should().Be(9);
    }

    #endregion

    #region GetRangeAsync Tests

    [Fact]
    public async Task GetRangeAsync_ShouldReturnSnapshotsInRange_WhenNoAggregation()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var snapshot1 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(5), workingSetMB: 100);
        var snapshot2 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(10), workingSetMB: 200);
        var snapshot3 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(15), workingSetMB: 300);
        var snapshotOutside = CreateTestSnapshot(timestamp: baseTime.AddMinutes(30), workingSetMB: 999);

        await _context.MetricSnapshots.AddRangeAsync(snapshot1, snapshot2, snapshot3, snapshotOutside);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRangeAsync(
            startTime: baseTime,
            endTime: baseTime.AddMinutes(20),
            aggregationMinutes: 0);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInAscendingOrder(r => r.Timestamp);
        result[0].WorkingSetMB.Should().Be(100);
        result[1].WorkingSetMB.Should().Be(200);
        result[2].WorkingSetMB.Should().Be(300);
        result.Should().NotContain(r => r.WorkingSetMB == 999);
    }

    [Fact]
    public async Task GetRangeAsync_ShouldReturnEmptyList_WhenNoSnapshotsInRange()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var snapshot = CreateTestSnapshot(timestamp: baseTime.AddDays(-1));
        await _context.MetricSnapshots.AddAsync(snapshot);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRangeAsync(
            startTime: baseTime,
            endTime: baseTime.AddHours(1),
            aggregationMinutes: 0);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRangeAsync_ShouldOrderByTimestamp_WhenReturningResults()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var snapshot1 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(15));
        var snapshot2 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(5));
        var snapshot3 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(10));

        // Add in non-sequential order
        await _context.MetricSnapshots.AddRangeAsync(snapshot1, snapshot2, snapshot3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRangeAsync(
            startTime: baseTime,
            endTime: baseTime.AddMinutes(20),
            aggregationMinutes: 0);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInAscendingOrder(r => r.Timestamp);
        result[0].Timestamp.Should().Be(baseTime.AddMinutes(5));
        result[1].Timestamp.Should().Be(baseTime.AddMinutes(10));
        result[2].Timestamp.Should().Be(baseTime.AddMinutes(15));
    }

    [Fact]
    public async Task GetRangeAsync_ShouldIncludeBoundarySnapshots_WhenExactlyAtStartOrEnd()
    {
        // Arrange
        var startTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 1, 15, 11, 0, 0, DateTimeKind.Utc);

        var snapshotAtStart = CreateTestSnapshot(timestamp: startTime, workingSetMB: 100);
        var snapshotAtEnd = CreateTestSnapshot(timestamp: endTime, workingSetMB: 200);
        var snapshotMiddle = CreateTestSnapshot(timestamp: startTime.AddMinutes(30), workingSetMB: 150);

        await _context.MetricSnapshots.AddRangeAsync(snapshotAtStart, snapshotAtEnd, snapshotMiddle);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRangeAsync(startTime, endTime, aggregationMinutes: 0);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(r => r.WorkingSetMB == 100);
        result.Should().Contain(r => r.WorkingSetMB == 200);
        result.Should().Contain(r => r.WorkingSetMB == 150);
    }

    #endregion

    #region GetRangeAsync Aggregation Tests

    [Fact]
    public async Task GetRangeAsync_ShouldAggregateSnapshots_When5MinuteAggregation()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        // First bucket: 10:00-10:05 (2 samples)
        var snapshot1 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(1), workingSetMB: 100, databaseAvgQueryTimeMs: 10);
        var snapshot2 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(3), workingSetMB: 200, databaseAvgQueryTimeMs: 20);

        // Second bucket: 10:05-10:10 (1 sample)
        var snapshot3 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(7), workingSetMB: 300, databaseAvgQueryTimeMs: 30);

        await _context.MetricSnapshots.AddRangeAsync(snapshot1, snapshot2, snapshot3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRangeAsync(
            startTime: baseTime,
            endTime: baseTime.AddMinutes(15),
            aggregationMinutes: 5);

        // Assert
        result.Should().HaveCount(2);
        result.Should().BeInAscendingOrder(r => r.Timestamp);

        // First bucket should average 100 and 200 = 150
        result[0].Timestamp.Should().Be(baseTime);
        result[0].WorkingSetMB.Should().Be(150);
        result[0].DatabaseAvgQueryTimeMs.Should().Be(15);

        // Second bucket should have 300
        result[1].Timestamp.Should().Be(baseTime.AddMinutes(5));
        result[1].WorkingSetMB.Should().Be(300);
        result[1].DatabaseAvgQueryTimeMs.Should().Be(30);
    }

    [Fact]
    public async Task GetRangeAsync_ShouldAverageAllMetrics_WhenAggregating()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var snapshot1 = CreateTestSnapshot(
            timestamp: baseTime.AddMinutes(1),
            databaseAvgQueryTimeMs: 10,
            workingSetMB: 100,
            heapSizeMB: 50,
            cacheHitRatePercent: 80,
            servicesRunningCount: 4,
            gen0Collections: 100,
            gen1Collections: 50,
            gen2Collections: 10);

        var snapshot2 = CreateTestSnapshot(
            timestamp: baseTime.AddMinutes(2),
            databaseAvgQueryTimeMs: 20,
            workingSetMB: 200,
            heapSizeMB: 60,
            cacheHitRatePercent: 90,
            servicesRunningCount: 6,
            gen0Collections: 200,
            gen1Collections: 100,
            gen2Collections: 20);

        await _context.MetricSnapshots.AddRangeAsync(snapshot1, snapshot2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetRangeAsync(
            startTime: baseTime,
            endTime: baseTime.AddMinutes(10),
            aggregationMinutes: 5);

        // Assert
        result.Should().HaveCount(1);
        result[0].DatabaseAvgQueryTimeMs.Should().Be(15); // (10 + 20) / 2
        result[0].WorkingSetMB.Should().Be(150); // (100 + 200) / 2
        result[0].HeapSizeMB.Should().Be(55); // (50 + 60) / 2
        result[0].CacheHitRatePercent.Should().Be(85); // (80 + 90) / 2
        result[0].ServicesRunningCount.Should().Be(5); // (4 + 6) / 2
        result[0].Gen0Collections.Should().Be(150); // (100 + 200) / 2
        result[0].Gen1Collections.Should().Be(75); // (50 + 100) / 2
        result[0].Gen2Collections.Should().Be(15); // (10 + 20) / 2
    }

    [Fact]
    public async Task GetRangeAsync_ShouldReturnEmpty_WhenNoSnapshotsAndAggregationRequested()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var result = await _repository.GetRangeAsync(
            startTime: baseTime,
            endTime: baseTime.AddHours(1),
            aggregationMinutes: 5);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRangeAsync_ShouldAlignBucketsToAggregationInterval_WhenAggregating()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        // Add samples at 10:02, 10:07, 10:12
        var snapshot1 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(2), workingSetMB: 100);
        var snapshot2 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(7), workingSetMB: 200);
        var snapshot3 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(12), workingSetMB: 300);

        await _context.MetricSnapshots.AddRangeAsync(snapshot1, snapshot2, snapshot3);
        await _context.SaveChangesAsync();

        // Act - 5 minute buckets
        var result = await _repository.GetRangeAsync(
            startTime: baseTime,
            endTime: baseTime.AddMinutes(15),
            aggregationMinutes: 5);

        // Assert
        result.Should().HaveCount(3);

        // Buckets should be at 10:00, 10:05, 10:10
        result[0].Timestamp.Should().Be(baseTime); // 10:00 bucket contains 10:02 sample
        result[1].Timestamp.Should().Be(baseTime.AddMinutes(5)); // 10:05 bucket contains 10:07 sample
        result[2].Timestamp.Should().Be(baseTime.AddMinutes(10)); // 10:10 bucket contains 10:12 sample
    }

    #endregion

    #region GetLatestAsync Tests

    [Fact]
    public async Task GetLatestAsync_ShouldReturnMostRecentSnapshot_WhenSnapshotsExist()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var snapshot1 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(5), workingSetMB: 100);
        var snapshot2 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(15), workingSetMB: 200);
        var snapshot3 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(10), workingSetMB: 150);

        await _context.MetricSnapshots.AddRangeAsync(snapshot1, snapshot2, snapshot3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetLatestAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Timestamp.Should().Be(baseTime.AddMinutes(15));
        result.WorkingSetMB.Should().Be(200);
    }

    [Fact]
    public async Task GetLatestAsync_ShouldReturnNull_WhenNoSnapshotsExist()
    {
        // Act
        var result = await _repository.GetLatestAsync();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestAsync_ShouldReturnLatestByTimestamp_NotByInsertionOrder()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        // Insert in non-chronological order
        var olderSnapshot = CreateTestSnapshot(timestamp: baseTime, workingSetMB: 100);
        await _context.MetricSnapshots.AddAsync(olderSnapshot);
        await _context.SaveChangesAsync();

        var newerSnapshot = CreateTestSnapshot(timestamp: baseTime.AddMinutes(-5), workingSetMB: 50);
        await _context.MetricSnapshots.AddAsync(newerSnapshot);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetLatestAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Timestamp.Should().Be(baseTime);
        result.WorkingSetMB.Should().Be(100);
    }

    #endregion

    #region DeleteOlderThanAsync Tests

    [Fact]
    public async Task DeleteOlderThanAsync_ShouldDeleteOldSnapshots_AndReturnCount()
    {
        // Arrange
        var cutoffDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var oldSnapshot1 = CreateTestSnapshot(timestamp: cutoffDate.AddHours(-2));
        var oldSnapshot2 = CreateTestSnapshot(timestamp: cutoffDate.AddHours(-1));
        var newSnapshot1 = CreateTestSnapshot(timestamp: cutoffDate.AddHours(1));
        var newSnapshot2 = CreateTestSnapshot(timestamp: cutoffDate.AddHours(2));

        await _context.MetricSnapshots.AddRangeAsync(oldSnapshot1, oldSnapshot2, newSnapshot1, newSnapshot2);
        await _context.SaveChangesAsync();

        // Act
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoffDate);

        // Assert
        deletedCount.Should().Be(2);

        // Verify only new snapshots remain
        var remaining = await _context.MetricSnapshots.ToListAsync();
        remaining.Should().HaveCount(2);
        remaining.Should().AllSatisfy(s => s.Timestamp.Should().BeOnOrAfter(cutoffDate));
    }

    [Fact]
    public async Task DeleteOlderThanAsync_ShouldNotDeleteSnapshotsAtCutoff_WhenExactMatch()
    {
        // Arrange
        var cutoffDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var oldSnapshot = CreateTestSnapshot(timestamp: cutoffDate.AddMinutes(-1));
        var snapshotAtCutoff = CreateTestSnapshot(timestamp: cutoffDate);
        var newSnapshot = CreateTestSnapshot(timestamp: cutoffDate.AddMinutes(1));

        await _context.MetricSnapshots.AddRangeAsync(oldSnapshot, snapshotAtCutoff, newSnapshot);
        await _context.SaveChangesAsync();

        // Act
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoffDate);

        // Assert
        deletedCount.Should().Be(1);

        var remaining = await _context.MetricSnapshots.ToListAsync();
        remaining.Should().HaveCount(2);
        remaining.Should().Contain(s => s.Timestamp == cutoffDate);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_ShouldReturnZero_WhenNoSnapshotsToDelete()
    {
        // Arrange
        var cutoffDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var newSnapshot = CreateTestSnapshot(timestamp: cutoffDate.AddHours(1));
        await _context.MetricSnapshots.AddAsync(newSnapshot);
        await _context.SaveChangesAsync();

        // Act
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoffDate);

        // Assert
        deletedCount.Should().Be(0);

        var remaining = await _context.MetricSnapshots.ToListAsync();
        remaining.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_ShouldReturnZero_WhenNoSnapshotsExist()
    {
        // Arrange
        var cutoffDate = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoffDate);

        // Assert
        deletedCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_ShouldHandleUtcConversion_WhenNonUtcDateProvided()
    {
        // Arrange
        var cutoffDateUnspecified = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);
        var cutoffDateUtc = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var oldSnapshot = CreateTestSnapshot(timestamp: cutoffDateUtc.AddHours(-1));
        var newSnapshot = CreateTestSnapshot(timestamp: cutoffDateUtc.AddHours(1));

        await _context.MetricSnapshots.AddRangeAsync(oldSnapshot, newSnapshot);
        await _context.SaveChangesAsync();

        // Act - Pass unspecified kind date
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoffDateUnspecified);

        // Assert - Should treat as UTC and delete old snapshot
        deletedCount.Should().Be(1);
    }

    #endregion

    #region GetCountAsync Tests

    [Fact]
    public async Task GetCountAsync_ShouldReturnCount_WhenSnapshotsInRange()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var snapshot1 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(5));
        var snapshot2 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(10));
        var snapshot3 = CreateTestSnapshot(timestamp: baseTime.AddMinutes(15));
        var snapshotOutside = CreateTestSnapshot(timestamp: baseTime.AddMinutes(30));

        await _context.MetricSnapshots.AddRangeAsync(snapshot1, snapshot2, snapshot3, snapshotOutside);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetCountAsync(
            startTime: baseTime,
            endTime: baseTime.AddMinutes(20));

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task GetCountAsync_ShouldReturnZero_WhenNoSnapshotsInRange()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var snapshot = CreateTestSnapshot(timestamp: baseTime.AddDays(-1));
        await _context.MetricSnapshots.AddAsync(snapshot);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetCountAsync(
            startTime: baseTime,
            endTime: baseTime.AddHours(1));

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetCountAsync_ShouldIncludeBoundarySnapshots_WhenExactlyAtStartOrEnd()
    {
        // Arrange
        var startTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var endTime = new DateTime(2024, 1, 15, 11, 0, 0, DateTimeKind.Utc);

        var snapshotAtStart = CreateTestSnapshot(timestamp: startTime);
        var snapshotAtEnd = CreateTestSnapshot(timestamp: endTime);
        var snapshotMiddle = CreateTestSnapshot(timestamp: startTime.AddMinutes(30));

        await _context.MetricSnapshots.AddRangeAsync(snapshotAtStart, snapshotAtEnd, snapshotMiddle);
        await _context.SaveChangesAsync();

        // Act
        var count = await _repository.GetCountAsync(startTime, endTime);

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public async Task GetCountAsync_ShouldReturnZero_WhenNoSnapshotsExist()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var count = await _repository.GetCountAsync(
            startTime: baseTime,
            endTime: baseTime.AddHours(1));

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetCountAsync_ShouldHandleUtcConversion_WhenNonUtcDatesProvided()
    {
        // Arrange
        var startTimeUnspecified = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Unspecified);
        var endTimeUnspecified = new DateTime(2024, 1, 15, 11, 0, 0, DateTimeKind.Unspecified);
        var startTimeUtc = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var snapshot = CreateTestSnapshot(timestamp: startTimeUtc.AddMinutes(30));
        await _context.MetricSnapshots.AddAsync(snapshot);
        await _context.SaveChangesAsync();

        // Act - Pass unspecified kind dates
        var count = await _repository.GetCountAsync(startTimeUnspecified, endTimeUnspecified);

        // Assert - Should treat as UTC and find the snapshot
        count.Should().Be(1);
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public async Task GetRangeAsync_ShouldHandleLargeDataset_WhenAggregating()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
        var snapshots = new List<MetricSnapshot>();

        // Create 100 snapshots over 100 minutes
        for (int i = 0; i < 100; i++)
        {
            snapshots.Add(CreateTestSnapshot(
                timestamp: baseTime.AddMinutes(i),
                workingSetMB: 100 + i));
        }

        await _context.MetricSnapshots.AddRangeAsync(snapshots);
        await _context.SaveChangesAsync();

        // Act - Aggregate into 10-minute buckets
        var result = await _repository.GetRangeAsync(
            startTime: baseTime,
            endTime: baseTime.AddMinutes(100),
            aggregationMinutes: 10);

        // Assert
        result.Should().HaveCount(10);
        result.Should().BeInAscendingOrder(r => r.Timestamp);
    }

    [Fact]
    public async Task AddAsync_GetLatest_DeleteOlder_IntegrationTest()
    {
        // Arrange
        var baseTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        // Add snapshots
        var snapshot1 = CreateTestSnapshot(timestamp: baseTime, workingSetMB: 100);
        var snapshot2 = CreateTestSnapshot(timestamp: baseTime.AddHours(1), workingSetMB: 200);
        var snapshot3 = CreateTestSnapshot(timestamp: baseTime.AddHours(2), workingSetMB: 300);

        await _repository.AddAsync(snapshot1);
        await _repository.AddAsync(snapshot2);
        await _repository.AddAsync(snapshot3);

        // Act & Assert - Get latest
        var latest = await _repository.GetLatestAsync();
        latest.Should().NotBeNull();
        latest!.WorkingSetMB.Should().Be(300);

        // Act & Assert - Delete older than 1.5 hours
        var deletedCount = await _repository.DeleteOlderThanAsync(baseTime.AddHours(1.5));
        deletedCount.Should().Be(2);

        // Verify only the newest remains
        var remaining = await _context.MetricSnapshots.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining[0].WorkingSetMB.Should().Be(300);
    }

    #endregion
}
