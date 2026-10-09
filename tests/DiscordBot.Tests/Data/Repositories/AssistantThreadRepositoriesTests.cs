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
/// <see cref="AssistantThreadRepository"/> and <see cref="AssistantThreadMessageRepository"/>
/// against the real database: the window read, the trim, the purge paths, and the retention
/// delete with its cascade.
/// </summary>
public class AssistantThreadRepositoriesTests : IDisposable
{
    private const ulong GuildId = 4100UL;
    private const ulong UserId = 4300UL;
    private const ulong OtherUserId = 4301UL;

    private readonly BotDbContext _context;
    private readonly TestDatabase _database;
    private readonly AssistantThreadRepository _threads;
    private readonly AssistantThreadMessageRepository _messages;

    public AssistantThreadRepositoriesTests()
    {
        (_context, _database) = TestDbContextFactory.CreateContext();
        _threads = new AssistantThreadRepository(
            _context, Mock.Of<ILogger<AssistantThreadRepository>>(), Mock.Of<ILogger<Repository<AssistantThread>>>());
        _messages = new AssistantThreadMessageRepository(
            _context, Mock.Of<ILogger<AssistantThreadMessageRepository>>(), Mock.Of<ILogger<Repository<AssistantThreadMessage>>>());

        _context.Guilds.Add(new Guild { Id = GuildId, Name = "Test Guild", JoinedAt = DateTime.UtcNow });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }

    private async Task<AssistantThread> SeedThreadAsync(ulong threadId, DateTime? lastActivity = null, ulong starter = UserId)
    {
        var thread = new AssistantThread
        {
            ThreadId = threadId,
            GuildId = GuildId,
            ParentChannelId = 55,
            StarterUserId = starter,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            LastActivityAt = lastActivity ?? DateTime.UtcNow
        };
        _context.AssistantThreads.Add(thread);
        await _context.SaveChangesAsync();
        return thread;
    }

    private async Task SeedTurnsAsync(ulong threadId, int count, ulong userId = UserId)
    {
        for (var i = 0; i < count; i++)
        {
            _context.AssistantThreadMessages.Add(new AssistantThreadMessage
            {
                ThreadId = threadId,
                UserId = userId,
                Role = i % 2 == 0 ? "user" : "assistant",
                Content = $"turn {i}",
                Timestamp = DateTime.UtcNow
            });
        }
        await _context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetByThreadId_ReturnsTheRow_OrNull()
    {
        await SeedThreadAsync(1);

        (await _threads.GetByThreadIdAsync(1)).Should().NotBeNull();
        (await _threads.GetByThreadIdAsync(2)).Should().BeNull();
    }

    [Fact]
    public async Task GetRecentByThread_ReturnsTheNewestTurns_OldestFirst()
    {
        await SeedThreadAsync(1);
        await SeedTurnsAsync(1, 10);

        var recent = await _messages.GetRecentByThreadAsync(1, 4);

        recent.Select(m => m.Content).Should().Equal("turn 6", "turn 7", "turn 8", "turn 9");
    }

    [Fact]
    public async Task GetRecentByThread_DoesNotMixThreads()
    {
        await SeedThreadAsync(1);
        await SeedThreadAsync(2);
        await SeedTurnsAsync(1, 2);
        await SeedTurnsAsync(2, 2);

        var recent = await _messages.GetRecentByThreadAsync(1, 10);

        recent.Should().HaveCount(2).And.OnlyContain(m => m.ThreadId == 1);
    }

    [Fact]
    public async Task DeleteOldestByThread_KeepsTheNewest_AndLeavesOtherThreadsAlone()
    {
        await SeedThreadAsync(1);
        await SeedThreadAsync(2);
        await SeedTurnsAsync(1, 10);
        await SeedTurnsAsync(2, 3);

        await _messages.DeleteOldestByThreadAsync(1, 4);

        var kept = await _context.AssistantThreadMessages.AsNoTracking().Where(m => m.ThreadId == 1).OrderBy(m => m.Id).ToListAsync();
        kept.Select(m => m.Content).Should().Equal("turn 6", "turn 7", "turn 8", "turn 9");
        (await _context.AssistantThreadMessages.CountAsync(m => m.ThreadId == 2)).Should().Be(3);
    }

    [Fact]
    public async Task DeleteByUser_RemovesOnlyThatUsersTurns()
    {
        await SeedThreadAsync(1);
        await SeedTurnsAsync(1, 4, UserId);
        await SeedTurnsAsync(1, 2, OtherUserId);

        var deleted = await _messages.DeleteByUserAsync(UserId);

        deleted.Should().Be(4);
        (await _context.AssistantThreadMessages.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task AnonymizeStarter_ZeroesThePointer_OnThatUsersThreadsOnly()
    {
        await SeedThreadAsync(1, starter: UserId);
        await SeedThreadAsync(2, starter: OtherUserId);

        var changed = await _threads.AnonymizeStarterAsync(UserId);

        changed.Should().Be(1);
        (await _context.AssistantThreads.AsNoTracking().SingleAsync(t => t.ThreadId == 1)).StarterUserId.Should().Be(0);
        (await _context.AssistantThreads.AsNoTracking().SingleAsync(t => t.ThreadId == 2)).StarterUserId.Should().Be(OtherUserId);
    }

    [Fact]
    public async Task DeleteInactiveOlderThan_RemovesOldThreadsWithTheirTurns_InBatches()
    {
        var old = DateTime.UtcNow.AddDays(-60);
        await SeedThreadAsync(1, old);
        await SeedThreadAsync(2, old.AddHours(1));
        await SeedThreadAsync(3, DateTime.UtcNow);
        await SeedTurnsAsync(1, 2);
        await SeedTurnsAsync(3, 2);
        var cutoff = DateTime.UtcNow.AddDays(-30);

        var first = await _threads.DeleteInactiveOlderThanAsync(cutoff, batchSize: 1);
        var second = await _threads.DeleteInactiveOlderThanAsync(cutoff, batchSize: 10);
        var third = await _threads.DeleteInactiveOlderThanAsync(cutoff, batchSize: 10);

        first.Should().Be(1, "the oldest goes first");
        second.Should().Be(1);
        third.Should().Be(0);
        (await _context.AssistantThreads.AsNoTracking().Select(t => t.ThreadId).ToListAsync()).Should().Equal(3UL);
        (await _context.AssistantThreadMessages.CountAsync(m => m.ThreadId == 1)).Should().Be(0, "turns cascade with the thread");
        (await _context.AssistantThreadMessages.CountAsync(m => m.ThreadId == 3)).Should().Be(2);
    }

    [Fact]
    public async Task ActiveSkills_RoundTripsThroughTheJsonColumn()
    {
        var thread = await SeedThreadAsync(1);
        thread.SetActiveSkillsList(new[] { "moderation", "analytics" });
        thread.Status = AssistantThreadStatus.Closed;
        await _threads.UpdateAsync(thread);

        var read = await _context.AssistantThreads.AsNoTracking().SingleAsync(t => t.ThreadId == 1);

        read.GetActiveSkillsList().Should().Equal("moderation", "analytics");
        read.Status.Should().Be(AssistantThreadStatus.Closed);
    }
}
