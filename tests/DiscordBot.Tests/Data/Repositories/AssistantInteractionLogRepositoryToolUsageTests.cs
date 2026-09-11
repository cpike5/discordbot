using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// Unit tests for <see cref="AssistantInteractionLogRepository.GetToolUsageAsync"/>, run against
/// the in-memory SQLite <see cref="TestDbContextFactory"/> so the projection-then-split shape is
/// exercised for real rather than over a list.
/// </summary>
public class AssistantInteractionLogRepositoryToolUsageTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly AssistantInteractionLogRepository _repository;

    private const ulong UserId = 9001UL;
    private const ulong GuildId = 9101UL;
    private const ulong OtherGuildId = 9102UL;

    private static readonly DateTime WindowStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc);

    private ulong _nextMessageId = 1;

    public AssistantInteractionLogRepositoryToolUsageTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _repository = new AssistantInteractionLogRepository(
            _context,
            Mock.Of<ILogger<AssistantInteractionLogRepository>>(),
            Mock.Of<ILogger<Repository<AssistantInteractionLog>>>());

        _context.Users.Add(new User { Id = UserId });
        _context.Guilds.Add(new Guild { Id = GuildId, Name = "Test Guild", JoinedAt = DateTime.UtcNow });
        _context.Guilds.Add(new Guild { Id = OtherGuildId, Name = "Other Guild", JoinedAt = DateTime.UtcNow });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void AddLog(DateTime timestamp, string? toolNames, bool success = true, ulong guildId = GuildId)
    {
        _context.AssistantInteractionLogs.Add(new AssistantInteractionLog
        {
            Timestamp = timestamp,
            UserId = UserId,
            GuildId = guildId,
            ChannelId = 9201UL,
            MessageId = _nextMessageId++,
            Question = "question",
            Response = "response",
            ToolNames = toolNames,
            Success = success
        });
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetToolUsageAsync_CountsEachCallButEachInteractionOnce()
    {
        AddLog(WindowStart.AddDays(1), "search_commands, search_commands, list_features");

        var usage = await _repository.GetToolUsageAsync(GuildId, WindowStart, WindowEnd);

        var search = usage.Single(u => u.ToolName == "search_commands");
        search.Calls.Should().Be(2);
        search.Interactions.Should().Be(1, "one question called it twice");

        usage.Single(u => u.ToolName == "list_features").Calls.Should().Be(1);
    }

    [Fact]
    public async Task GetToolUsageAsync_OrdersByCallsDescending()
    {
        AddLog(WindowStart.AddDays(1), "list_features");
        AddLog(WindowStart.AddDays(2), "search_commands, search_commands");

        var usage = await _repository.GetToolUsageAsync(GuildId, WindowStart, WindowEnd);

        usage.Select(u => u.ToolName).Should().Equal("search_commands", "list_features");
    }

    [Fact]
    public async Task GetToolUsageAsync_CountsAFailedInteractionAgainstEveryToolItCalled()
    {
        AddLog(WindowStart.AddDays(1), "search_commands", success: false);
        AddLog(WindowStart.AddDays(2), "search_commands", success: true);

        var usage = await _repository.GetToolUsageAsync(GuildId, WindowStart, WindowEnd);

        var search = usage.Single(u => u.ToolName == "search_commands");
        search.Interactions.Should().Be(2);
        search.FailedInteractions.Should().Be(1);
    }

    [Fact]
    public async Task GetToolUsageAsync_ReportsTheLatestUse()
    {
        AddLog(WindowStart.AddDays(1), "search_commands");
        AddLog(WindowStart.AddDays(5), "search_commands");
        AddLog(WindowStart.AddDays(3), "search_commands");

        var usage = await _repository.GetToolUsageAsync(GuildId, WindowStart, WindowEnd);

        usage.Single().LastUsed.Should().Be(WindowStart.AddDays(5));
    }

    [Fact]
    public async Task GetToolUsageAsync_ExcludesOtherGuildsAndRowsOutsideTheWindow()
    {
        AddLog(WindowStart.AddDays(1), "search_commands", guildId: OtherGuildId);
        AddLog(WindowStart.AddDays(-1), "search_commands");
        AddLog(WindowEnd.AddDays(1), "search_commands");
        AddLog(WindowStart.AddDays(2), "list_features");

        var usage = await _repository.GetToolUsageAsync(GuildId, WindowStart, WindowEnd);

        usage.Select(u => u.ToolName).Should().Equal("list_features");
    }

    [Fact]
    public async Task GetToolUsageAsync_IgnoresRowsThatNamedNoTool()
    {
        AddLog(WindowStart.AddDays(1), null);
        AddLog(WindowStart.AddDays(2), "");

        var usage = await _repository.GetToolUsageAsync(GuildId, WindowStart, WindowEnd);

        usage.Should().BeEmpty();
    }

    [Fact]
    public async Task GetToolUsageAsync_ReturnsNoRowForAToolThatWasNeverCalled()
    {
        // The zeroes come from pairing this with the tool catalogue on the page; the repository
        // reports only what the log actually contains.
        AddLog(WindowStart.AddDays(1), "search_commands");

        var usage = await _repository.GetToolUsageAsync(GuildId, WindowStart, WindowEnd);

        usage.Should().ContainSingle();
    }
}
