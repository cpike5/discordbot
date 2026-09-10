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
/// Unit tests for the batched <see cref="AssistantInteractionLogRepository.DeleteOlderThanAsync(DateTime, int, CancellationToken)"/>
/// overload added for <c>AssistantInteractionLogRetentionService</c>'s per-batch sweep loop, run
/// against the in-memory SQLite <see cref="TestDbContextFactory"/> so the id-select-then-
/// <c>ExecuteDeleteAsync</c> shape is exercised for real.
/// </summary>
public class AssistantInteractionLogRepositoryBatchDeleteTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly AssistantInteractionLogRepository _repository;

    private const ulong UserId = 9001UL;
    private const ulong GuildId = 9101UL;

    public AssistantInteractionLogRepositoryBatchDeleteTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _repository = new AssistantInteractionLogRepository(
            _context,
            Mock.Of<ILogger<AssistantInteractionLogRepository>>(),
            Mock.Of<ILogger<Repository<AssistantInteractionLog>>>());

        _context.Users.Add(new User { Id = UserId });
        _context.Guilds.Add(new Guild { Id = GuildId, Name = "Test Guild", JoinedAt = DateTime.UtcNow });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static AssistantInteractionLog CreateLog(DateTime timestamp, ulong messageId) => new()
    {
        Timestamp = timestamp,
        UserId = UserId,
        GuildId = GuildId,
        ChannelId = 9201UL,
        MessageId = messageId,
        Question = "question",
        Response = "response",
        Success = true
    };

    [Fact]
    public async Task DeleteOlderThanAsync_WithBatchSize_RemovesOnlyOneBatchAndOnlyRowsBeforeCutoff()
    {
        var old = DateTime.UtcNow.AddDays(-100);
        var recent = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            _context.AssistantInteractionLogs.Add(CreateLog(old, (ulong)(1000 + i)));
        }
        _context.AssistantInteractionLogs.Add(CreateLog(recent, 2000UL));
        await _context.SaveChangesAsync();

        var deleted = await _repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-1), batchSize: 2);

        deleted.Should().Be(2, "only one batch's worth should delete per call");
        (await _context.AssistantInteractionLogs.CountAsync()).Should().Be(4, "5 old + 1 recent, minus the 2 deleted");
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithBatchSize_ReturnsZero_WhenNothingIsOlderThanCutoff()
    {
        _context.AssistantInteractionLogs.Add(CreateLog(DateTime.UtcNow, 3000UL));
        await _context.SaveChangesAsync();

        var deleted = await _repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-1), batchSize: 100);

        deleted.Should().Be(0);
        (await _context.AssistantInteractionLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithBatchSize_ClampsOversizedBatchSize_AndStillDeletesEverythingOlder()
    {
        var old = DateTime.UtcNow.AddDays(-100);
        _context.AssistantInteractionLogs.Add(CreateLog(old, 4000UL));
        _context.AssistantInteractionLogs.Add(CreateLog(old, 4001UL));
        await _context.SaveChangesAsync();

        // A caller-supplied batchSize far above the 1000 clamp must not throw or change the result.
        var deleted = await _repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-1), batchSize: 50_000);

        deleted.Should().Be(2);
        (await _context.AssistantInteractionLogs.CountAsync()).Should().Be(0);
    }
}
