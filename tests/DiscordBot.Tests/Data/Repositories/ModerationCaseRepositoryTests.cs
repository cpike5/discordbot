using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

public class ModerationCaseRepositoryTests : IDisposable
{
    private const ulong GuildId = 123456789UL;
    private const ulong ModeratorId = 222UL;

    private readonly BotDbContext _context;
    private readonly TestDatabase _database;
    private readonly ModerationCaseRepository _repository;
    private long _nextCaseNumber = 1;

    public ModerationCaseRepositoryTests()
    {
        (_context, _database) = TestDbContextFactory.CreateContext();
        _repository = new ModerationCaseRepository(
            _context,
            Mock.Of<ILogger<ModerationCaseRepository>>(),
            Mock.Of<ILogger<Repository<ModerationCase>>>());
        _context.Guilds.Add(new Guild { Id = GuildId, Name = "Test", JoinedAt = DateTime.UtcNow, IsActive = true });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }

    private ModerationCase AddCase(ulong targetUserId, CaseType type, DateTime createdAt, DateTime? expiresAt = null)
    {
        var moderationCase = new ModerationCase
        {
            Id = Guid.NewGuid(),
            CaseNumber = _nextCaseNumber++,
            GuildId = GuildId,
            TargetUserId = targetUserId,
            ModeratorUserId = ModeratorId,
            Type = type,
            CreatedAt = createdAt,
            Duration = expiresAt.HasValue ? expiresAt - createdAt : null,
            ExpiresAt = expiresAt
        };
        _context.ModerationCases.Add(moderationCase);
        return moderationCase;
    }

    [Fact]
    public async Task GetExpiredCasesAsync_ReturnsOnlyExpiredBansThatNoLaterBanOrUnbanSupersedes()
    {
        var now = DateTime.UtcNow;

        // Expired temp ban with nothing after it: due to be lifted.
        var due = AddCase(1UL, CaseType.Ban, now.AddHours(-2), now.AddMinutes(-1));

        // Expired temp ban already lifted (by hand or by the expiry service): closed.
        AddCase(2UL, CaseType.Ban, now.AddHours(-2), now.AddMinutes(-1));
        AddCase(2UL, CaseType.Unban, now.AddHours(-1));

        // Expired temp ban later replaced by a permanent ban: must not be lifted.
        AddCase(3UL, CaseType.Ban, now.AddHours(-2), now.AddMinutes(-1));
        AddCase(3UL, CaseType.Ban, now.AddHours(-1));

        // Temp ban not yet expired.
        AddCase(4UL, CaseType.Ban, now.AddHours(-2), now.AddHours(1));

        // Expired mute: Discord timeouts lift themselves, so the query leaves mutes out.
        AddCase(5UL, CaseType.Mute, now.AddHours(-2), now.AddMinutes(-1));

        // An unban that predates the ban does not close it.
        AddCase(6UL, CaseType.Unban, now.AddHours(-3));
        var dueAfterEarlierUnban = AddCase(6UL, CaseType.Ban, now.AddHours(-2), now.AddMinutes(-1));

        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var expired = (await _repository.GetExpiredCasesAsync(now)).ToList();

        expired.Select(c => c.Id).Should().BeEquivalentTo(new[] { due.Id, dueAfterEarlierUnban.Id });
    }
}
