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

public class FlaggedEventRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly TestDatabase _database;
    private readonly FlaggedEventRepository _repository;

    public FlaggedEventRepositoryTests()
    {
        (_context, _database) = TestDbContextFactory.CreateContext();
        _repository = new FlaggedEventRepository(
            _context,
            Mock.Of<ILogger<FlaggedEventRepository>>(),
            Mock.Of<ILogger<Repository<FlaggedEvent>>>());
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }

    [Fact]
    public async Task UpdateAsync_OfSeveralEventsLoadedThroughOneContext_UpdatesEachOfThem()
    {
        // A bulk review loads and updates one event after another in the same request. Each load
        // brings the Guild with it, and the second update used to fail on the Guild the first left tracked.
        var guild = new Guild { Id = 123456789UL, Name = "Test", JoinedAt = DateTime.UtcNow, IsActive = true };
        var events = Enumerable.Range(0, 3).Select(i => new FlaggedEvent
        {
            Id = Guid.NewGuid(),
            GuildId = guild.Id,
            UserId = 111UL,
            RuleType = RuleType.Spam,
            Severity = Severity.Low,
            Description = $"Event {i}",
            Evidence = "{}",
            Status = FlaggedEventStatus.Pending,
            CreatedAt = DateTime.UtcNow
        }).ToList();
        _context.Guilds.Add(guild);
        _context.FlaggedEvents.AddRange(events);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        foreach (var id in events.Select(e => e.Id))
        {
            var loaded = await _repository.GetByIdAsync(id);
            loaded!.Status = FlaggedEventStatus.Dismissed;
            await _repository.UpdateAsync(loaded);
        }

        _context.ChangeTracker.Clear();
        var statuses = await _context.FlaggedEvents.Select(e => e.Status).ToListAsync();
        statuses.Should().HaveCount(3).And.OnlyContain(s => s == FlaggedEventStatus.Dismissed);
    }
}
