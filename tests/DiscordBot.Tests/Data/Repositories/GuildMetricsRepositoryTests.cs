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
/// Unit tests for GuildMetricsRepository.
/// </summary>
public class GuildMetricsRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly GuildMetricsRepository _repository;
    private readonly Mock<ILogger<GuildMetricsRepository>> _mockLogger;
    private readonly Mock<ILogger<Repository<GuildMetricsSnapshot>>> _mockBaseLogger;

    public GuildMetricsRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _mockLogger = new Mock<ILogger<GuildMetricsRepository>>();
        _mockBaseLogger = new Mock<ILogger<Repository<GuildMetricsSnapshot>>>();
        _repository = new GuildMetricsRepository(_context, _mockLogger.Object, _mockBaseLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// Helper method to create and save a Guild entity for testing.
    /// </summary>
    private async Task CreateTestGuildAsync(ulong guildId)
    {
        var guild = new Guild
        {
            Id = guildId,
            Name = $"Test Guild {guildId}",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };
        await _context.Guilds.AddAsync(guild);
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetByDateRangeAsync_ReturnsCorrectDateRange()
    {
        // Arrange
        var guildId = 123456789UL;
        var startDate = new DateOnly(2024, 1, 5);
        var endDate = new DateOnly(2024, 1, 15);

        await CreateTestGuildAsync(guildId);

        // Snapshot within range
        var snapshot1 = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = new DateOnly(2024, 1, 7),
            TotalMembers = 100,
            ActiveMembers = 50,
            MembersJoined = 5,
            MembersLeft = 2,
            TotalMessages = 1000,
            CommandsExecuted = 50,
            ModerationActions = 3,
            ActiveChannels = 10,
            TotalVoiceMinutes = 200,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot2 = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = new DateOnly(2024, 1, 10),
            TotalMembers = 105,
            ActiveMembers = 55,
            MembersJoined = 3,
            MembersLeft = 1,
            TotalMessages = 1200,
            CommandsExecuted = 60,
            ModerationActions = 2,
            ActiveChannels = 12,
            TotalVoiceMinutes = 250,
            CreatedAt = DateTime.UtcNow
        };

        // Snapshot outside range (too early)
        var snapshotTooEarly = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = new DateOnly(2024, 1, 1),
            TotalMembers = 95,
            ActiveMembers = 45,
            MembersJoined = 2,
            MembersLeft = 0,
            TotalMessages = 800,
            CommandsExecuted = 40,
            ModerationActions = 1,
            ActiveChannels = 8,
            TotalVoiceMinutes = 150,
            CreatedAt = DateTime.UtcNow
        };

        // Snapshot outside range (too late)
        var snapshotTooLate = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = new DateOnly(2024, 1, 20),
            TotalMembers = 110,
            ActiveMembers = 60,
            MembersJoined = 4,
            MembersLeft = 1,
            TotalMessages = 1500,
            CommandsExecuted = 70,
            ModerationActions = 4,
            ActiveChannels = 15,
            TotalVoiceMinutes = 300,
            CreatedAt = DateTime.UtcNow
        };

        await _context.GuildMetricsSnapshots.AddRangeAsync(snapshot1, snapshot2, snapshotTooEarly, snapshotTooLate);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDateRangeAsync(guildId, startDate, endDate);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(s => s.SnapshotDate == new DateOnly(2024, 1, 7));
        result.Should().Contain(s => s.SnapshotDate == new DateOnly(2024, 1, 10));
        result.Should().NotContain(s => s.SnapshotDate == new DateOnly(2024, 1, 1));
        result.Should().NotContain(s => s.SnapshotDate == new DateOnly(2024, 1, 20));
        result.Should().BeInAscendingOrder(s => s.SnapshotDate);
    }

    [Fact]
    public async Task GetByDateRangeAsync_WithSingleDate_ReturnsSingleSnapshot()
    {
        // Arrange
        var guildId = 123456789UL;
        var date = new DateOnly(2024, 1, 15);

        await CreateTestGuildAsync(guildId);

        var snapshot = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = date,
            TotalMembers = 100,
            ActiveMembers = 50,
            MembersJoined = 5,
            MembersLeft = 2,
            TotalMessages = 1000,
            CommandsExecuted = 50,
            ModerationActions = 3,
            ActiveChannels = 10,
            TotalVoiceMinutes = 200,
            CreatedAt = DateTime.UtcNow
        };

        await _context.GuildMetricsSnapshots.AddAsync(snapshot);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDateRangeAsync(guildId, date, date);

        // Assert
        result.Should().HaveCount(1);
        result[0].SnapshotDate.Should().Be(date);
    }

    [Fact]
    public async Task GetByDateRangeAsync_WithNoSnapshots_ReturnsEmptyList()
    {
        // Arrange
        var guildId = 123456789UL;
        var startDate = new DateOnly(2024, 1, 1);
        var endDate = new DateOnly(2024, 1, 31);

        // Act
        var result = await _repository.GetByDateRangeAsync(guildId, startDate, endDate);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsLatestSnapshot()
    {
        // Arrange
        var guildId = 123456789UL;

        await CreateTestGuildAsync(guildId);

        var snapshot1 = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = new DateOnly(2024, 1, 1),
            TotalMembers = 100,
            ActiveMembers = 50,
            MembersJoined = 5,
            MembersLeft = 2,
            TotalMessages = 1000,
            CommandsExecuted = 50,
            ModerationActions = 3,
            ActiveChannels = 10,
            TotalVoiceMinutes = 200,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot2 = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = new DateOnly(2024, 1, 15),
            TotalMembers = 110,
            ActiveMembers = 60,
            MembersJoined = 7,
            MembersLeft = 3,
            TotalMessages = 1500,
            CommandsExecuted = 70,
            ModerationActions = 4,
            ActiveChannels = 12,
            TotalVoiceMinutes = 300,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot3 = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = new DateOnly(2024, 1, 10),
            TotalMembers = 105,
            ActiveMembers = 55,
            MembersJoined = 6,
            MembersLeft = 2,
            TotalMessages = 1200,
            CommandsExecuted = 60,
            ModerationActions = 3,
            ActiveChannels = 11,
            TotalVoiceMinutes = 250,
            CreatedAt = DateTime.UtcNow
        };

        await _context.GuildMetricsSnapshots.AddRangeAsync(snapshot1, snapshot2, snapshot3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetLatestAsync(guildId);

        // Assert
        result.Should().NotBeNull();
        result!.SnapshotDate.Should().Be(new DateOnly(2024, 1, 15));
        result.TotalMembers.Should().Be(110);
    }

    [Fact]
    public async Task GetLatestAsync_WithNoSnapshots_ReturnsNull()
    {
        // Arrange
        var guildId = 123456789UL;

        // Act
        var result = await _repository.GetLatestAsync(guildId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetGrowthTimeSeriesAsync_CalculatesGrowthCorrectly()
    {
        // Arrange
        var guildId = 123456789UL;
        var startDate = new DateOnly(2024, 1, 1);
        var endDate = new DateOnly(2024, 1, 5);

        await CreateTestGuildAsync(guildId);

        var snapshot1 = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = new DateOnly(2024, 1, 1),
            TotalMembers = 100,
            ActiveMembers = 50,
            MembersJoined = 10,
            MembersLeft = 2,
            TotalMessages = 1000,
            CommandsExecuted = 50,
            ModerationActions = 3,
            ActiveChannels = 10,
            TotalVoiceMinutes = 200,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot2 = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = new DateOnly(2024, 1, 3),
            TotalMembers = 105,
            ActiveMembers = 55,
            MembersJoined = 5,
            MembersLeft = 3,
            TotalMessages = 1200,
            CommandsExecuted = 60,
            ModerationActions = 2,
            ActiveChannels = 12,
            TotalVoiceMinutes = 250,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot3 = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = new DateOnly(2024, 1, 5),
            TotalMembers = 110,
            ActiveMembers = 60,
            MembersJoined = 8,
            MembersLeft = 5,
            TotalMessages = 1500,
            CommandsExecuted = 70,
            ModerationActions = 4,
            ActiveChannels = 15,
            TotalVoiceMinutes = 300,
            CreatedAt = DateTime.UtcNow
        };

        await _context.GuildMetricsSnapshots.AddRangeAsync(snapshot1, snapshot2, snapshot3);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetGrowthTimeSeriesAsync(guildId, startDate, endDate);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInAscendingOrder(r => r.Date);

        var day1 = result.First(r => r.Date == new DateOnly(2024, 1, 1));
        day1.NetGrowth.Should().Be(8); // 10 - 2
        day1.Joined.Should().Be(10);
        day1.Left.Should().Be(2);

        var day3 = result.First(r => r.Date == new DateOnly(2024, 1, 3));
        day3.NetGrowth.Should().Be(2); // 5 - 3
        day3.Joined.Should().Be(5);
        day3.Left.Should().Be(3);

        var day5 = result.First(r => r.Date == new DateOnly(2024, 1, 5));
        day5.NetGrowth.Should().Be(3); // 8 - 5
        day5.Joined.Should().Be(8);
        day5.Left.Should().Be(5);
    }

    [Fact]
    public async Task GetGrowthTimeSeriesAsync_WithNegativeGrowth_ReturnsCorrectValues()
    {
        // Arrange
        var guildId = 123456789UL;
        var startDate = new DateOnly(2024, 1, 1);
        var endDate = new DateOnly(2024, 1, 2);

        await CreateTestGuildAsync(guildId);

        var snapshot = new GuildMetricsSnapshot
        {
            GuildId = guildId,
            SnapshotDate = new DateOnly(2024, 1, 1),
            TotalMembers = 100,
            ActiveMembers = 50,
            MembersJoined = 2,
            MembersLeft = 10,
            TotalMessages = 1000,
            CommandsExecuted = 50,
            ModerationActions = 3,
            ActiveChannels = 10,
            TotalVoiceMinutes = 200,
            CreatedAt = DateTime.UtcNow
        };

        await _context.GuildMetricsSnapshots.AddAsync(snapshot);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetGrowthTimeSeriesAsync(guildId, startDate, endDate);

        // Assert
        result.Should().HaveCount(1);
        result[0].NetGrowth.Should().Be(-8); // 2 - 10
        result[0].Joined.Should().Be(2);
        result[0].Left.Should().Be(10);
    }

    [Fact]
    public async Task GetGrowthTimeSeriesAsync_WithNoData_ReturnsEmptyList()
    {
        // Arrange
        var guildId = 123456789UL;
        var startDate = new DateOnly(2024, 1, 1);
        var endDate = new DateOnly(2024, 1, 31);

        // Act
        var result = await _repository.GetGrowthTimeSeriesAsync(guildId, startDate, endDate);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task UpsertAsync_WithNewSnapshot_CreatesRecord()
    {
        // Arrange
        await CreateTestGuildAsync(123456789UL);

        var snapshot = new GuildMetricsSnapshot
        {
            GuildId = 123456789UL,
            SnapshotDate = new DateOnly(2024, 1, 15),
            TotalMembers = 150,
            ActiveMembers = 75,
            MembersJoined = 10,
            MembersLeft = 3,
            TotalMessages = 2000,
            CommandsExecuted = 100,
            ModerationActions = 5,
            ActiveChannels = 20,
            TotalVoiceMinutes = 500
        };

        // Act
        await _repository.UpsertAsync(snapshot);

        // Assert
        var saved = await _context.GuildMetricsSnapshots.FindAsync(snapshot.Id);
        saved.Should().NotBeNull();
        saved!.TotalMembers.Should().Be(150);
        saved.ActiveMembers.Should().Be(75);
        saved.MembersJoined.Should().Be(10);
        saved.MembersLeft.Should().Be(3);
        saved.TotalMessages.Should().Be(2000);
        saved.CommandsExecuted.Should().Be(100);
        saved.ModerationActions.Should().Be(5);
        saved.ActiveChannels.Should().Be(20);
        saved.TotalVoiceMinutes.Should().Be(500);
        saved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpsertAsync_WithExistingSnapshot_UpdatesRecord()
    {
        // Arrange
        await CreateTestGuildAsync(123456789UL);

        var existing = new GuildMetricsSnapshot
        {
            GuildId = 123456789UL,
            SnapshotDate = new DateOnly(2024, 1, 15),
            TotalMembers = 100,
            ActiveMembers = 50,
            MembersJoined = 5,
            MembersLeft = 2,
            TotalMessages = 1000,
            CommandsExecuted = 50,
            ModerationActions = 3,
            ActiveChannels = 10,
            TotalVoiceMinutes = 200,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        await _context.GuildMetricsSnapshots.AddAsync(existing);
        await _context.SaveChangesAsync();

        var existingId = existing.Id;

        // Detach to simulate fresh update
        _context.Entry(existing).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

        var updated = new GuildMetricsSnapshot
        {
            GuildId = 123456789UL,
            SnapshotDate = new DateOnly(2024, 1, 15),
            TotalMembers = 150,
            ActiveMembers = 80,
            MembersJoined = 12,
            MembersLeft = 4,
            TotalMessages = 2500,
            CommandsExecuted = 120,
            ModerationActions = 8,
            ActiveChannels = 25,
            TotalVoiceMinutes = 600
        };

        // Act
        await _repository.UpsertAsync(updated);

        // Assert
        var saved = await _context.GuildMetricsSnapshots.FindAsync(existingId);
        saved.Should().NotBeNull();
        saved!.TotalMembers.Should().Be(150);
        saved.ActiveMembers.Should().Be(80);
        saved.MembersJoined.Should().Be(12);
        saved.MembersLeft.Should().Be(4);
        saved.TotalMessages.Should().Be(2500);
        saved.CommandsExecuted.Should().Be(120);
        saved.ModerationActions.Should().Be(8);
        saved.ActiveChannels.Should().Be(25);
        saved.TotalVoiceMinutes.Should().Be(600);
        saved.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify only one record exists
        var count = _context.GuildMetricsSnapshots.Count();
        count.Should().Be(1);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesOldRecordsInBatches()
    {
        // Arrange
        var cutoff = new DateOnly(2024, 1, 15);

        await CreateTestGuildAsync(123456789UL);

        // Old records that should be deleted
        var oldSnapshot1 = new GuildMetricsSnapshot
        {
            GuildId = 123456789UL,
            SnapshotDate = new DateOnly(2024, 1, 1),
            TotalMembers = 100,
            ActiveMembers = 50,
            MembersJoined = 5,
            MembersLeft = 2,
            TotalMessages = 1000,
            CommandsExecuted = 50,
            ModerationActions = 3,
            ActiveChannels = 10,
            TotalVoiceMinutes = 200,
            CreatedAt = DateTime.UtcNow
        };

        var oldSnapshot2 = new GuildMetricsSnapshot
        {
            GuildId = 123456789UL,
            SnapshotDate = new DateOnly(2024, 1, 10),
            TotalMembers = 105,
            ActiveMembers = 55,
            MembersJoined = 6,
            MembersLeft = 3,
            TotalMessages = 1200,
            CommandsExecuted = 60,
            ModerationActions = 4,
            ActiveChannels = 12,
            TotalVoiceMinutes = 250,
            CreatedAt = DateTime.UtcNow
        };

        // Recent record that should be kept
        var recentSnapshot = new GuildMetricsSnapshot
        {
            GuildId = 123456789UL,
            SnapshotDate = new DateOnly(2024, 1, 20),
            TotalMembers = 110,
            ActiveMembers = 60,
            MembersJoined = 7,
            MembersLeft = 4,
            TotalMessages = 1500,
            CommandsExecuted = 70,
            ModerationActions = 5,
            ActiveChannels = 15,
            TotalVoiceMinutes = 300,
            CreatedAt = DateTime.UtcNow
        };

        await _context.GuildMetricsSnapshots.AddRangeAsync(oldSnapshot1, oldSnapshot2, recentSnapshot);
        await _context.SaveChangesAsync();

        // Act
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoff, batchSize: 1000);

        // Assert
        deletedCount.Should().Be(2);

        var remaining = await _context.GuildMetricsSnapshots.ToListAsync();
        remaining.Should().HaveCount(1);
        remaining[0].SnapshotDate.Should().Be(new DateOnly(2024, 1, 20));
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithBatchSize_DeletesOnlyBatchSize()
    {
        // Arrange
        var cutoff = new DateOnly(2024, 1, 15);

        await CreateTestGuildAsync(123456789UL);

        for (int i = 1; i <= 5; i++)
        {
            var snapshot = new GuildMetricsSnapshot
            {
                GuildId = 123456789UL,
                SnapshotDate = new DateOnly(2024, 1, i),
                TotalMembers = 100,
                ActiveMembers = 50,
                MembersJoined = 5,
                MembersLeft = 2,
                TotalMessages = 1000,
                CommandsExecuted = 50,
                ModerationActions = 3,
                ActiveChannels = 10,
                TotalVoiceMinutes = 200,
                CreatedAt = DateTime.UtcNow
            };
            await _context.GuildMetricsSnapshots.AddAsync(snapshot);
        }

        await _context.SaveChangesAsync();

        // Act - Delete only 2 at a time
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoff, batchSize: 2);

        // Assert
        deletedCount.Should().Be(2);

        var remaining = _context.GuildMetricsSnapshots.Count();
        remaining.Should().Be(3);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithNoOldRecords_ReturnsZero()
    {
        // Arrange
        var cutoff = new DateOnly(2024, 1, 15);

        await CreateTestGuildAsync(123456789UL);

        var recentSnapshot = new GuildMetricsSnapshot
        {
            GuildId = 123456789UL,
            SnapshotDate = new DateOnly(2024, 1, 20),
            TotalMembers = 100,
            ActiveMembers = 50,
            MembersJoined = 5,
            MembersLeft = 2,
            TotalMessages = 1000,
            CommandsExecuted = 50,
            ModerationActions = 3,
            ActiveChannels = 10,
            TotalVoiceMinutes = 200,
            CreatedAt = DateTime.UtcNow
        };

        await _context.GuildMetricsSnapshots.AddAsync(recentSnapshot);
        await _context.SaveChangesAsync();

        // Act
        var deletedCount = await _repository.DeleteOlderThanAsync(cutoff, batchSize: 1000);

        // Assert
        deletedCount.Should().Be(0);

        var remaining = _context.GuildMetricsSnapshots.Count();
        remaining.Should().Be(1);
    }

    [Fact]
    public async Task GetByDateRangeAsync_FiltersByGuildId()
    {
        // Arrange
        var guildId1 = 123456789UL;
        var guildId2 = 987654321UL;
        var startDate = new DateOnly(2024, 1, 1);
        var endDate = new DateOnly(2024, 1, 31);

        await CreateTestGuildAsync(guildId1);
        await CreateTestGuildAsync(guildId2);

        var snapshot1 = new GuildMetricsSnapshot
        {
            GuildId = guildId1,
            SnapshotDate = new DateOnly(2024, 1, 15),
            TotalMembers = 100,
            ActiveMembers = 50,
            MembersJoined = 5,
            MembersLeft = 2,
            TotalMessages = 1000,
            CommandsExecuted = 50,
            ModerationActions = 3,
            ActiveChannels = 10,
            TotalVoiceMinutes = 200,
            CreatedAt = DateTime.UtcNow
        };

        var snapshot2 = new GuildMetricsSnapshot
        {
            GuildId = guildId2,
            SnapshotDate = new DateOnly(2024, 1, 15),
            TotalMembers = 200,
            ActiveMembers = 100,
            MembersJoined = 10,
            MembersLeft = 5,
            TotalMessages = 2000,
            CommandsExecuted = 100,
            ModerationActions = 6,
            ActiveChannels = 20,
            TotalVoiceMinutes = 400,
            CreatedAt = DateTime.UtcNow
        };

        await _context.GuildMetricsSnapshots.AddRangeAsync(snapshot1, snapshot2);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByDateRangeAsync(guildId1, startDate, endDate);

        // Assert
        result.Should().HaveCount(1);
        result[0].GuildId.Should().Be(guildId1);
        result[0].TotalMembers.Should().Be(100);
    }
}
