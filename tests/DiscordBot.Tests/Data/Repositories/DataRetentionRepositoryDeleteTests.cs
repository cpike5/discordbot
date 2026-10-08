using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// Tests for the batched delete methods <c>DataRetentionService</c> sweeps with, one per table,
/// run against real PostgreSQL. Each test seeds rows on both sides of the cutoff and checks
/// that only the older ones go.
/// </summary>
public class DataRetentionRepositoryDeleteTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly TestDatabase _database;

    private const ulong UserId = 7001UL;
    private const ulong GuildId = 7101UL;

    private static readonly DateTime Old = DateTime.UtcNow.AddDays(-100);
    private static readonly DateTime Recent = DateTime.UtcNow.AddDays(-1);
    private static readonly DateTime Cutoff = DateTime.UtcNow.AddDays(-30);

    public DataRetentionRepositoryDeleteTests()
    {
        (_context, _database) = TestDbContextFactory.CreateContext();

        _context.Users.Add(new User { Id = UserId, Username = "user" });
        _context.Guilds.Add(new Guild { Id = GuildId, Name = "Test Guild", JoinedAt = DateTime.UtcNow });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }

    [Fact]
    public async Task CommandLogRepository_DeleteOlderThanAsync_DeletesOnlyRowsBeforeCutoff()
    {
        var repository = new CommandLogRepository(
            _context, Mock.Of<ILogger<CommandLogRepository>>(), Mock.Of<ILogger<Repository<CommandLog>>>());

        CommandLog Log(DateTime at) => new()
        {
            Id = Guid.NewGuid(), GuildId = GuildId, UserId = UserId, CommandName = "ping", ExecutedAt = at, Success = true
        };
        _context.CommandLogs.AddRange(Log(Old), Log(Old), Log(Old), Log(Recent));
        await _context.SaveChangesAsync();

        var firstBatch = await repository.DeleteOlderThanAsync(Cutoff, batchSize: 2);
        var secondBatch = await repository.DeleteOlderThanAsync(Cutoff, batchSize: 2);

        firstBatch.Should().Be(2, "one call deletes at most one batch");
        secondBatch.Should().Be(1);
        var remaining = await _context.CommandLogs.AsNoTracking().ToListAsync();
        remaining.Should().ContainSingle().Which.ExecutedAt.Should().BeAfter(Cutoff);
    }

    [Fact]
    public async Task UserActivityEventRepository_DeleteOlderThanAsync_DeletesOnlyRowsBeforeCutoff()
    {
        var repository = new UserActivityEventRepository(
            _context, Mock.Of<ILogger<UserActivityEventRepository>>(), Mock.Of<ILogger<Repository<UserActivityEvent>>>());

        UserActivityEvent Event(DateTime at) => new()
        {
            UserId = UserId, GuildId = GuildId, ChannelId = 1UL, Timestamp = at, LoggedAt = at, EventType = ActivityEventType.Message
        };
        _context.UserActivityEvents.AddRange(Event(Old), Event(Old), Event(Old), Event(Recent));
        await _context.SaveChangesAsync();

        var firstBatch = await repository.DeleteOlderThanAsync(Cutoff, batchSize: 2);
        var secondBatch = await repository.DeleteOlderThanAsync(Cutoff, batchSize: 2);

        firstBatch.Should().Be(2, "one call deletes at most one batch");
        secondBatch.Should().Be(1);
        var remaining = await _context.UserActivityEvents.AsNoTracking().ToListAsync();
        remaining.Should().ContainSingle().Which.LoggedAt.Should().BeAfter(Cutoff);
    }

    [Fact]
    public async Task ConnectionEventRepository_DeleteOlderThanAsync_DeletesOnlyRowsBeforeCutoff()
    {
        var repository = CreateConnectionEventRepository();

        ConnectionEvent Event(DateTime at) => new() { EventType = "Connected", Timestamp = at };
        _context.ConnectionEvents.AddRange(Event(Old), Event(Old), Event(Old), Event(Recent));
        await _context.SaveChangesAsync();

        var firstBatch = await repository.DeleteOlderThanAsync(Cutoff, batchSize: 2);
        var secondBatch = await repository.DeleteOlderThanAsync(Cutoff, batchSize: 2);

        firstBatch.Should().Be(2, "one call deletes at most one batch");
        secondBatch.Should().Be(1);
        var remaining = await _context.ConnectionEvents.AsNoTracking().ToListAsync();
        remaining.Should().ContainSingle().Which.Timestamp.Should().BeAfter(Cutoff);
    }

    [Fact]
    public async Task ConnectionEventRepository_CleanupOldEventsAsync_DeletesEverythingOlderThanRetention()
    {
        var repository = CreateConnectionEventRepository();

        ConnectionEvent Event(DateTime at) => new() { EventType = "Disconnected", Timestamp = at };
        _context.ConnectionEvents.AddRange(Event(Old), Event(Old), Event(Recent));
        await _context.SaveChangesAsync();

        var deleted = await repository.CleanupOldEventsAsync(retentionDays: 30);

        deleted.Should().Be(2);
        var remaining = await _context.ConnectionEvents.AsNoTracking().ToListAsync();
        remaining.Should().ContainSingle().Which.Timestamp.Should().BeAfter(Cutoff);
    }

    [Fact]
    public async Task TtsMessageRepository_DeleteOlderThanAsync_DeletesOnlyRowsBeforeCutoff()
    {
        var repository = new TtsMessageRepository(_context, Mock.Of<ILogger<TtsMessageRepository>>());

        TtsMessage Message(DateTime at) => new()
        {
            Id = Guid.NewGuid(), GuildId = GuildId, UserId = UserId, Username = "user", Message = "hello",
            Voice = "en-US-JennyNeural", DurationSeconds = 1.0, CreatedAt = at
        };
        _context.TtsMessages.AddRange(Message(Old), Message(Old), Message(Old), Message(Recent));
        await _context.SaveChangesAsync();

        var firstBatch = await repository.DeleteOlderThanAsync(Cutoff, batchSize: 2);
        var secondBatch = await repository.DeleteOlderThanAsync(Cutoff, batchSize: 2);

        firstBatch.Should().Be(2, "one call deletes at most one batch");
        secondBatch.Should().Be(1);
        var remaining = await _context.TtsMessages.AsNoTracking().ToListAsync();
        remaining.Should().ContainSingle().Which.CreatedAt.Should().BeAfter(Cutoff);
    }

    [Fact]
    public async Task AssistantUsageMetricsRepository_DeleteOlderThanAsync_WithBatchSize_DeletesOnlyRowsBeforeCutoff()
    {
        var repository = new AssistantUsageMetricsRepository(
            _context, Mock.Of<ILogger<AssistantUsageMetricsRepository>>(), Mock.Of<ILogger<Repository<AssistantUsageMetrics>>>());

        // (GuildId, Date) is unique, so each row gets its own day.
        AssistantUsageMetrics Metrics(DateTime date) => new()
        {
            GuildId = GuildId, Date = date.Date, UpdatedAt = date
        };
        _context.AssistantUsageMetrics.AddRange(
            Metrics(Old), Metrics(Old.AddDays(1)), Metrics(Old.AddDays(2)), Metrics(Recent));
        await _context.SaveChangesAsync();

        var firstBatch = await repository.DeleteOlderThanAsync(Cutoff, batchSize: 2);
        var secondBatch = await repository.DeleteOlderThanAsync(Cutoff, batchSize: 2);

        firstBatch.Should().Be(2, "one call deletes at most one batch");
        secondBatch.Should().Be(1);
        var remaining = await _context.AssistantUsageMetrics.AsNoTracking().ToListAsync();
        remaining.Should().ContainSingle().Which.Date.Should().Be(Recent.Date);
    }

    [Fact]
    public async Task AudioPlaybackLogRepository_DeleteOlderThanAsync_DeletesOnlyRowsBeforeCutoff()
    {
        var repository = new AudioPlaybackLogRepository(_context, Mock.Of<ILogger<Repository<AudioPlaybackLog>>>());

        AudioPlaybackLog Log(DateTime at) => new()
        {
            GuildId = GuildId, UserId = UserId, FeatureType = AudioFeatureType.Soundboard, ContentName = "airhorn", PlayedAt = at
        };
        _context.AudioPlaybackLogs.AddRange(Log(Old), Log(Old), Log(Old), Log(Recent));
        await _context.SaveChangesAsync();

        var firstBatch = await repository.DeleteOlderThanAsync(Cutoff, batchSize: 2);
        var secondBatch = await repository.DeleteOlderThanAsync(Cutoff, batchSize: 2);

        firstBatch.Should().Be(2, "one call deletes at most one batch");
        secondBatch.Should().Be(1);
        var remaining = await _context.AudioPlaybackLogs.AsNoTracking().ToListAsync();
        remaining.Should().ContainSingle().Which.PlayedAt.Should().BeAfter(Cutoff);
    }

    private ConnectionEventRepository CreateConnectionEventRepository() =>
        new(_context, Mock.Of<ILogger<ConnectionEventRepository>>(), Mock.Of<ILogger<Repository<ConnectionEvent>>>());
}
