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
/// Unit tests for the batched <see cref="DmAssistantInteractionLogRepository.DeleteOlderThanAsync(DateTime, int, CancellationToken)"/>
/// overload added for <c>AssistantInteractionLogRetentionService</c>'s per-batch sweep loop, run
/// against the in-memory SQLite <see cref="TestDbContextFactory"/> so the id-select-then-
/// <c>ExecuteDeleteAsync</c> shape is exercised for real.
/// </summary>
public class DmAssistantInteractionLogRepositoryBatchDeleteTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly DmAssistantInteractionLogRepository _repository;

    private const ulong UserId = 9301UL;

    public DmAssistantInteractionLogRepositoryBatchDeleteTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _repository = new DmAssistantInteractionLogRepository(
            _context,
            Mock.Of<ILogger<DmAssistantInteractionLogRepository>>(),
            Mock.Of<ILogger<Repository<DmAssistantInteractionLog>>>());

        _context.Users.Add(new User { Id = UserId });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static DmAssistantInteractionLog CreateLog(DateTime timestamp) => new()
    {
        Timestamp = timestamp,
        UserId = UserId,
        Message = "hi",
        Response = "hello",
        Success = true
    };

    [Fact]
    public async Task DeleteOlderThanAsync_WithBatchSize_RemovesOnlyOneBatchAndOnlyRowsBeforeCutoff()
    {
        var old = DateTime.UtcNow.AddDays(-100);
        var recent = DateTime.UtcNow;

        for (var i = 0; i < 5; i++)
        {
            _context.DmAssistantInteractionLogs.Add(CreateLog(old));
        }
        _context.DmAssistantInteractionLogs.Add(CreateLog(recent));
        await _context.SaveChangesAsync();

        var deleted = await _repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-1), batchSize: 2);

        deleted.Should().Be(2, "only one batch's worth should delete per call");
        (await _context.DmAssistantInteractionLogs.CountAsync()).Should().Be(4, "5 old + 1 recent, minus the 2 deleted");
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithBatchSize_ReturnsZero_WhenNothingIsOlderThanCutoff()
    {
        _context.DmAssistantInteractionLogs.Add(CreateLog(DateTime.UtcNow));
        await _context.SaveChangesAsync();

        var deleted = await _repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-1), batchSize: 100);

        deleted.Should().Be(0);
        (await _context.DmAssistantInteractionLogs.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_WithBatchSize_ClampsOversizedBatchSize_AndStillDeletesEverythingOlder()
    {
        var old = DateTime.UtcNow.AddDays(-100);
        _context.DmAssistantInteractionLogs.Add(CreateLog(old));
        _context.DmAssistantInteractionLogs.Add(CreateLog(old));
        await _context.SaveChangesAsync();

        // A caller-supplied batchSize far above the 1000 clamp must not throw or change the result.
        var deleted = await _repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-1), batchSize: 50_000);

        deleted.Should().Be(2);
        (await _context.DmAssistantInteractionLogs.CountAsync()).Should().Be(0);
    }
}
