using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Data.Repositories;

/// <summary>
/// Opt-in verification of <see cref="LlmUsageRepository"/>'s grouped cost aggregates against a
/// real PostgreSQL server — the SQLite in-memory tests in <see cref="LlmUsageRepositoryTests"/>
/// say nothing about the Npgsql provider's translation of the <c>Sum((double)r.CostUsd)</c> cast.
/// Skipped unless <c>POSTGRES_TEST_CONNECTION</c> is set, to a database already migrated with
/// <c>dotnet ef database update --context PostgresBotDbContext</c> (see CLAUDE.md). Each test
/// clears the <see cref="LlmUsageRecord"/> table before and after itself, so point it at a
/// throwaway database, never production.
/// </summary>
[Trait("Category", "Postgres")]
public class LlmUsageRepositoryPostgresTests : IAsyncLifetime
{
    private static readonly string? ConnectionString = Environment.GetEnvironmentVariable("POSTGRES_TEST_CONNECTION");

    private PostgresBotDbContext? _context;
    private LlmUsageRepository? _repository;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        var options = new DbContextOptionsBuilder<PostgresBotDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        _context = new PostgresBotDbContext(options);
        // Schema is applied out-of-band (dotnet ef database update --context PostgresBotDbContext)
        // against the throwaway database POSTGRES_TEST_CONNECTION points at - the test-owned role
        // need not carry CREATEDB, only DML on an already-migrated schema. Clear any rows a prior
        // run left behind rather than recreating the database.
        await _context.Set<LlmUsageRecord>().ExecuteDeleteAsync();

        _repository = new LlmUsageRepository(
            _context,
            Mock.Of<ILogger<LlmUsageRepository>>(),
            Mock.Of<ILogger<Repository<LlmUsageRecord>>>());
    }

    public async Task DisposeAsync()
    {
        if (_context != null)
        {
            await _context.Set<LlmUsageRecord>().ExecuteDeleteAsync();
            await _context.DisposeAsync();
        }
    }

    private static LlmUsageRecord CreateRecord(
        ulong userId, ulong? guildId, LlmMode mode, string model, decimal cost,
        LlmCostSource costSource, DateTime timestamp, int inputTokens = 100, int outputTokens = 50) => new()
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
        Success = true,
        LatencyMs = 200,
        LlmCalls = 1
    };

    [Fact]
    public async Task GetTotalsAsync_SumsCostAcrossProviders_OnRealPostgres()
    {
        // xunit v2 has no runtime Skip.If; POSTGRES_TEST_CONNECTION unset just short-circuits to a
        // trivial pass rather than exercising real Postgres. CI never sets it, so this is a no-op there.
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        var now = DateTime.UtcNow;
        _context!.Set<LlmUsageRecord>().AddRange(
            CreateRecord(1, 100, LlmMode.GuildAssistant, "model/a", 0.123456789m, LlmCostSource.Billed, now.AddMinutes(-5)),
            CreateRecord(2, 100, LlmMode.GuildAssistant, "model/b", 0.000000015m, LlmCostSource.Estimated, now.AddMinutes(-3)));
        await _context.SaveChangesAsync();

        var totals = await _repository!.GetTotalsAsync(new LlmUsageQuery { From = now.AddHours(-1), To = now.AddHours(1) });

        totals.MessageCount.Should().Be(2);
        totals.CostUsd.Should().BeApproximately(0.123456804m, 0.00000001m);
    }

    [Fact]
    public async Task GetByUserAsync_GetByModelAsync_GetByModeAsync_GetByDayAsync_RunWithoutNotSupportedException_OnRealPostgres()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        var now = DateTime.UtcNow;
        _context!.Set<LlmUsageRecord>().AddRange(
            CreateRecord(1, 100, LlmMode.GuildAssistant, "model/a", 1.5m, LlmCostSource.Billed, now.AddMinutes(-5)),
            CreateRecord(2, 100, LlmMode.DmAssistant, "model/b", 0.25m, LlmCostSource.Estimated, now.AddMinutes(-3)));
        await _context.SaveChangesAsync();

        var query = new LlmUsageQuery { From = now.AddHours(-1), To = now.AddHours(1) };

        var byUser = await _repository!.GetByUserAsync(query, take: 10);
        var byModel = await _repository.GetByModelAsync(query);
        var byMode = await _repository.GetByModeAsync(query);
        var byDay = await _repository.GetByDayAsync(query);
        var paged = await _repository.GetRecordsAsync(query, page: 1, pageSize: 10);

        byUser.Should().HaveCount(2);
        byModel.Should().HaveCount(2);
        byMode.Should().HaveCount(2);
        byDay.Should().HaveCount(1);
        paged.TotalCount.Should().Be(2);
    }
}
