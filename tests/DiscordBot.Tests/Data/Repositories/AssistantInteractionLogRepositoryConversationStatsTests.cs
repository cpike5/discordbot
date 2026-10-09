using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// <see cref="AssistantInteractionLogRepository.GetConversationStatsAsync"/>: distinct threads and
/// their turns in a window, counted from the log's <c>ThreadId</c>.
/// </summary>
public class AssistantInteractionLogRepositoryConversationStatsTests : IDisposable
{
    private const ulong GuildId = 5100UL;
    private const ulong OtherGuildId = 5101UL;
    private const ulong UserId = 5300UL;

    private readonly BotDbContext _context;
    private readonly TestDatabase _database;
    private readonly AssistantInteractionLogRepository _repository;

    public AssistantInteractionLogRepositoryConversationStatsTests()
    {
        (_context, _database) = TestDbContextFactory.CreateContext();
        _repository = new AssistantInteractionLogRepository(
            _context, Mock.Of<ILogger<AssistantInteractionLogRepository>>(), Mock.Of<ILogger<Repository<AssistantInteractionLog>>>());

        _context.Guilds.Add(new Guild { Id = GuildId, Name = "Guild", JoinedAt = DateTime.UtcNow });
        _context.Guilds.Add(new Guild { Id = OtherGuildId, Name = "Other", JoinedAt = DateTime.UtcNow });
        _context.Users.Add(new User { Id = UserId });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
    }

    private void Log(ulong? threadId, DateTime? at = null, ulong guildId = GuildId) =>
        _context.AssistantInteractionLogs.Add(new AssistantInteractionLog
        {
            Timestamp = at ?? DateTime.UtcNow,
            UserId = UserId,
            GuildId = guildId,
            ChannelId = threadId ?? 1,
            MessageId = 1,
            Question = "q",
            Response = "a",
            Success = true,
            ThreadId = threadId
        });

    [Fact]
    public async Task NoThreadTurns_IsEmpty()
    {
        Log(null);
        Log(null);
        await _context.SaveChangesAsync();

        var stats = await _repository.GetConversationStatsAsync(GuildId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        stats.Should().Be(AssistantConversationStats.Empty);
        stats.AverageTurns.Should().Be(0);
    }

    [Fact]
    public async Task CountsDistinctThreadsAndTheirTurns_IgnoringSingleReplies()
    {
        Log(null);
        Log(10); Log(10); Log(10);
        Log(20);
        await _context.SaveChangesAsync();

        var stats = await _repository.GetConversationStatsAsync(GuildId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1));

        stats.Conversations.Should().Be(2);
        stats.Turns.Should().Be(4);
        stats.AverageTurns.Should().Be(2.0);
    }

    [Fact]
    public async Task RespectsTheWindowAndTheGuild()
    {
        Log(10, DateTime.UtcNow.AddDays(-40));
        Log(20);
        Log(30, guildId: OtherGuildId);
        await _context.SaveChangesAsync();

        var stats = await _repository.GetConversationStatsAsync(GuildId, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(1));

        stats.Conversations.Should().Be(1);
        stats.Turns.Should().Be(1);
    }
}
