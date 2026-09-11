using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// Unit tests for <see cref="LlmUsageRepository"/>'s grouped queries, run against the in-memory
/// SQLite <see cref="TestDbContextFactory"/> so the GroupBy → Select translation is exercised for
/// real rather than assumed.
/// </summary>
public class LlmUsageRepositoryTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly LlmUsageRepository _repository;

    private const ulong UserA = 1001UL;
    private const ulong UserB = 1002UL;
    private const ulong GuildA = 2001UL;
    private const ulong GuildB = 2002UL;

    public LlmUsageRepositoryTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _repository = new LlmUsageRepository(
            _context,
            Mock.Of<ILogger<LlmUsageRepository>>(),
            Mock.Of<ILogger<Repository<LlmUsageRecord>>>());
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static LlmUsageRecord CreateRecord(
        ulong userId,
        ulong? guildId,
        LlmMode mode,
        string model,
        decimal cost,
        LlmCostSource costSource,
        DateTime timestamp,
        int inputTokens = 100,
        int outputTokens = 50,
        bool success = true,
        int latencyMs = 200) => new()
    {
        Timestamp = timestamp,
        Mode = mode,
        UserId = userId,
        GuildId = guildId,
        Model = model,
        InputTokens = inputTokens,
        OutputTokens = outputTokens,
        CostUsd = cost,
        CostSource = costSource,
        Success = success,
        LatencyMs = latencyMs,
        LlmCalls = 1,
        ToolCalls = 0
    };

    [Fact]
    public async Task AddRangeAsync_WithEmptyCollection_DoesNotThrow()
    {
        await _repository.AddRangeAsync(Array.Empty<LlmUsageRecord>());

        (await _context.LlmUsageRecords.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetTotalsAsync_AggregatesTokensCostAndBilledShare()
    {
        var now = DateTime.UtcNow;
        await _repository.AddRangeAsync(new[]
        {
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, now, inputTokens: 100, outputTokens: 50),
            CreateRecord(UserB, GuildA, LlmMode.GuildAssistant, "model/a", 0.05m, LlmCostSource.Estimated, now, inputTokens: 50, outputTokens: 25, success: false)
        });

        var totals = await _repository.GetTotalsAsync(new LlmUsageQuery { From = now.AddMinutes(-1), To = now.AddMinutes(1) });

        totals.MessageCount.Should().Be(2);
        totals.InputTokens.Should().Be(150);
        totals.OutputTokens.Should().Be(75);
        totals.CostUsd.Should().Be(0.15m);
        totals.FailedCount.Should().Be(1);
        totals.BilledCostShare.Should().NotBeNull();
        totals.BilledCostShare!.Value.Should().BeApproximately((double)(0.10m / 0.15m), 0.0001);
    }

    [Fact]
    public async Task GetTotalsAsync_WithNoMatchingRows_ReturnsEmptyTotals()
    {
        var now = DateTime.UtcNow;

        var totals = await _repository.GetTotalsAsync(new LlmUsageQuery { From = now.AddDays(-1), To = now });

        totals.MessageCount.Should().Be(0);
        totals.CostUsd.Should().Be(0);
    }

    [Fact]
    public async Task GetByUserAsync_OrdersByCostDescending_AndComputesShare()
    {
        var now = DateTime.UtcNow;
        await _repository.AddRangeAsync(new[]
        {
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.30m, LlmCostSource.Billed, now),
            CreateRecord(UserB, GuildA, LlmMode.GuildAssistant, "model/a", 0.70m, LlmCostSource.Billed, now)
        });

        var byUser = await _repository.GetByUserAsync(
            new LlmUsageQuery { From = now.AddMinutes(-1), To = now.AddMinutes(1) }, take: 10);

        byUser.Should().HaveCount(2);
        byUser[0].UserId.Should().Be(UserB);
        byUser[0].CostUsd.Should().Be(0.70m);
        byUser[0].CostShare.Should().BeApproximately(0.7, 0.0001);
        byUser[1].UserId.Should().Be(UserA);
        byUser[1].CostShare.Should().BeApproximately(0.3, 0.0001);
    }

    [Fact]
    public async Task GetByUserAsync_RespectsTakeLimit()
    {
        var now = DateTime.UtcNow;
        await _repository.AddRangeAsync(new[]
        {
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, now),
            CreateRecord(UserB, GuildA, LlmMode.GuildAssistant, "model/a", 0.20m, LlmCostSource.Billed, now)
        });

        var byUser = await _repository.GetByUserAsync(
            new LlmUsageQuery { From = now.AddMinutes(-1), To = now.AddMinutes(1) }, take: 1);

        byUser.Should().HaveCount(1);
        byUser[0].UserId.Should().Be(UserB); // higher cost first
    }

    [Fact]
    public async Task GetByModelAsync_GroupsByModel()
    {
        var now = DateTime.UtcNow;
        await _repository.AddRangeAsync(new[]
        {
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, now),
            CreateRecord(UserB, GuildA, LlmMode.GuildAssistant, "model/a", 0.20m, LlmCostSource.Billed, now),
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/b", 0.05m, LlmCostSource.Billed, now)
        });

        var byModel = await _repository.GetByModelAsync(new LlmUsageQuery { From = now.AddMinutes(-1), To = now.AddMinutes(1) });

        byModel.Should().HaveCount(2);
        var modelA = byModel.Single(m => m.Model == "model/a");
        modelA.MessageCount.Should().Be(2);
        modelA.CostUsd.Should().Be(0.30m);
    }

    [Fact]
    public async Task GetByModeAsync_GroupsByMode()
    {
        var now = DateTime.UtcNow;
        await _repository.AddRangeAsync(new[]
        {
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, now),
            CreateRecord(UserA, null, LlmMode.DmAssistant, "model/a", 0.20m, LlmCostSource.Billed, now)
        });

        var byMode = await _repository.GetByModeAsync(new LlmUsageQuery { From = now.AddMinutes(-1), To = now.AddMinutes(1) });

        byMode.Should().HaveCount(2);
        byMode.Should().Contain(m => m.Mode == LlmMode.GuildAssistant && m.CostUsd == 0.10m);
        byMode.Should().Contain(m => m.Mode == LlmMode.DmAssistant && m.CostUsd == 0.20m);
    }

    [Fact]
    public async Task GetByDayAsync_GroupsByCalendarDay()
    {
        var day1 = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var day1Later = new DateTime(2026, 1, 1, 20, 0, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2026, 1, 2, 5, 0, 0, DateTimeKind.Utc);

        await _repository.AddRangeAsync(new[]
        {
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, day1),
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.20m, LlmCostSource.Billed, day1Later),
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.30m, LlmCostSource.Billed, day2)
        });

        var byDay = await _repository.GetByDayAsync(new LlmUsageQuery { From = day1.AddDays(-1), To = day2.AddDays(1) });

        byDay.Should().HaveCount(2);
        byDay[0].Day.Should().Be(day1.Date);
        byDay[0].MessageCount.Should().Be(2);
        byDay[0].CostUsd.Should().Be(0.30m);
        byDay[1].Day.Should().Be(day2.Date);
        byDay[1].CostUsd.Should().Be(0.30m);
    }

    [Fact]
    public async Task GetTotalsAsync_FiltersByGuildAndMode()
    {
        var now = DateTime.UtcNow;
        await _repository.AddRangeAsync(new[]
        {
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, now),
            CreateRecord(UserA, GuildB, LlmMode.GuildAssistant, "model/a", 0.20m, LlmCostSource.Billed, now),
            CreateRecord(UserA, GuildA, LlmMode.DmAssistant, "model/a", 0.30m, LlmCostSource.Billed, now)
        });

        var guildFiltered = await _repository.GetTotalsAsync(new LlmUsageQuery
        {
            From = now.AddMinutes(-1),
            To = now.AddMinutes(1),
            GuildId = GuildA
        });
        guildFiltered.MessageCount.Should().Be(2);
        guildFiltered.CostUsd.Should().Be(0.40m);

        var modeFiltered = await _repository.GetTotalsAsync(new LlmUsageQuery
        {
            From = now.AddMinutes(-1),
            To = now.AddMinutes(1),
            Mode = LlmMode.DmAssistant
        });
        modeFiltered.MessageCount.Should().Be(1);
        modeFiltered.CostUsd.Should().Be(0.30m);
    }

    [Fact]
    public async Task GetRecordsAsync_ReturnsNewestFirst_AndTotalCount()
    {
        var now = DateTime.UtcNow;
        await _repository.AddRangeAsync(new[]
        {
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, now.AddMinutes(-2)),
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.20m, LlmCostSource.Billed, now.AddMinutes(-1)),
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.30m, LlmCostSource.Billed, now)
        });

        var page = await _repository.GetRecordsAsync(
            new LlmUsageQuery { From = now.AddMinutes(-5), To = now.AddMinutes(1) }, page: 1, pageSize: 2);

        page.TotalCount.Should().Be(3);
        page.Records.Should().HaveCount(2);
        page.Records[0].CostUsd.Should().Be(0.30m); // newest first
        page.Records[1].CostUsd.Should().Be(0.20m);
    }

    [Fact]
    public async Task GetRecordsAsync_WithIdenticalTimestamps_PagesStably_ByIdTiebreak()
    {
        // Three rows sharing one Timestamp: without a tiebreak, OrderByDescending(Timestamp) alone
        // does not guarantee a stable order across pages, so a row can be skipped or repeated
        // between page 1 and page 2. Inserted out of id order so id and cost line up predictably.
        var now = DateTime.UtcNow;
        await _repository.AddRangeAsync(new[]
        {
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, now),
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.20m, LlmCostSource.Billed, now),
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.30m, LlmCostSource.Billed, now)
        });

        var query = new LlmUsageQuery { From = now.AddMinutes(-1), To = now.AddMinutes(1) };
        var page1 = await _repository.GetRecordsAsync(query, page: 1, pageSize: 2);
        var page2 = await _repository.GetRecordsAsync(query, page: 2, pageSize: 2);

        var allIds = page1.Records.Select(r => r.Id).Concat(page2.Records.Select(r => r.Id)).ToList();
        allIds.Should().OnlyHaveUniqueItems();
        allIds.Should().HaveCount(3);
        // ThenByDescending(Id) means the highest id (last inserted) leads each page.
        page1.Records.Select(r => r.Id).Should().BeInDescendingOrder();
        page2.Records.Select(r => r.Id).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task DeleteOlderThanAsync_RemovesOnlyRowsBeforeCutoff()
    {
        var old = DateTime.UtcNow.AddDays(-100);
        var recent = DateTime.UtcNow;

        await _repository.AddRangeAsync(new[]
        {
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, old),
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.20m, LlmCostSource.Billed, recent)
        });

        var deleted = await _repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-1), batchSize: 100);

        deleted.Should().Be(1);
        (await _context.LlmUsageRecords.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task DeleteOlderThanAsync_ClampsOversizedBatchSize_AndStillDeletesEverythingOlder()
    {
        var old = DateTime.UtcNow.AddDays(-100);
        await _repository.AddRangeAsync(new[]
        {
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, old),
            CreateRecord(UserB, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, old)
        });

        // A caller-supplied batchSize far above the 1000 clamp must not throw or change the result.
        var deleted = await _repository.DeleteOlderThanAsync(DateTime.UtcNow.AddDays(-1), batchSize: 50_000);

        deleted.Should().Be(2);
        (await _context.LlmUsageRecords.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeleteByUserAsync_RemovesAllRowsForThatUserOnly()
    {
        var now = DateTime.UtcNow;
        await _repository.AddRangeAsync(new[]
        {
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, now),
            CreateRecord(UserA, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, now),
            CreateRecord(UserB, GuildA, LlmMode.GuildAssistant, "model/a", 0.10m, LlmCostSource.Billed, now)
        });

        var deleted = await _repository.DeleteByUserAsync(UserA);

        deleted.Should().Be(2);
        (await _repository.CountByUserAsync(UserA)).Should().Be(0);
        (await _repository.CountByUserAsync(UserB)).Should().Be(1);
    }
}
