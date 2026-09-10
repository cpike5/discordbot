using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for the <see cref="LlmUsageRecord"/> ledger. Grouped queries are done
/// in SQL (GroupBy → Select) and avoid provider-specific functions so both the SQLite and
/// PostgreSQL providers behave identically; day grouping uses <c>Timestamp.Date</c>, which both
/// translate.
/// </summary>
public class LlmUsageRepository : Repository<LlmUsageRecord>, ILlmUsageRepository
{
    private readonly ILogger<LlmUsageRepository> _logger;

    public LlmUsageRepository(
        BotDbContext context,
        ILogger<LlmUsageRepository> logger,
        ILogger<Repository<LlmUsageRecord>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AddRangeAsync(IReadOnlyCollection<LlmUsageRecord> records, CancellationToken cancellationToken = default)
    {
        if (records.Count == 0)
        {
            return;
        }

        await DbSet.AddRangeAsync(records, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Inserted {Count} LLM usage record(s)", records.Count);
    }

    /// <inheritdoc />
    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken = default)
    {
        // Clamped to 1000: SQLite's default compiled-in limit on bound parameters/expression-tree
        // terms (SQLITE_MAX_VARIABLE_NUMBER-adjacent limits) makes a huge IN (...) list of ids -
        // built below from idsToDelete - unreliable well before batchSize reaches five figures. 1000
        // is comfortably under that ceiling on both providers and still a large batch.
        batchSize = Math.Clamp(batchSize, 1, 1000);
        var totalDeleted = 0;

        while (true)
        {
            var idsToDelete = await DbSet
                .Where(r => r.Timestamp < cutoff)
                .OrderBy(r => r.Id)
                .Select(r => r.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (idsToDelete.Count == 0)
            {
                break;
            }

            var deleted = await DbSet
                .Where(r => idsToDelete.Contains(r.Id))
                .ExecuteDeleteAsync(cancellationToken);

            totalDeleted += deleted;

            if (idsToDelete.Count < batchSize)
            {
                break;
            }
        }

        _logger.LogInformation("Deleted {Count} LLM usage record(s) older than {Cutoff}", totalDeleted, cutoff);
        return totalDeleted;
    }

    /// <inheritdoc />
    public Task<int> DeleteByUserAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .Where(r => r.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountByUserAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .AsNoTracking()
            .CountAsync(r => r.UserId == userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LlmUsageTotals> GetTotalsAsync(LlmUsageQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = Filter(query);

        var totals = await filtered
            .GroupBy(r => 1)
            .Select(g => new
            {
                MessageCount = g.Count(),
                InputTokens = g.Sum(r => (long)r.InputTokens),
                OutputTokens = g.Sum(r => (long)r.OutputTokens),
                CachedTokens = g.Sum(r => (long)r.CachedTokens),
                CacheWriteTokens = g.Sum(r => (long)r.CacheWriteTokens),
                // Summed as double, not decimal: SQLite's EF provider has no native decimal type
                // and refuses to translate Sum(decimal) into SQL at all (throws NotSupportedException).
                // Casting through double keeps one query shape working on both providers.
                CostUsd = Math.Round((decimal)g.Sum(r => (double)r.CostUsd), 8),
                BilledCostUsd = Math.Round((decimal)g.Where(r => r.CostSource == Core.Enums.LlmCostSource.Billed).Sum(r => (double)r.CostUsd), 8),
                FailedCount = g.Count(r => !r.Success),
                AverageLatencyMs = g.Average(r => (double)r.LatencyMs)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (totals == null || totals.MessageCount == 0)
        {
            return new LlmUsageTotals();
        }

        return new LlmUsageTotals
        {
            MessageCount = totals.MessageCount,
            InputTokens = totals.InputTokens,
            OutputTokens = totals.OutputTokens,
            CachedTokens = totals.CachedTokens,
            CacheWriteTokens = totals.CacheWriteTokens,
            CostUsd = totals.CostUsd,
            BilledCostShare = totals.CostUsd > 0 ? (double)(totals.BilledCostUsd / totals.CostUsd) : 0,
            FailedCount = totals.FailedCount,
            AverageLatencyMs = totals.AverageLatencyMs
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmUsageByUser>> GetByUserAsync(LlmUsageQuery query, int take, CancellationToken cancellationToken = default)
    {
        var filtered = Filter(query);

        // SQLite's EF provider refuses to translate Sum(decimal) at all (it has no native decimal
        // type); summing as double and casting back keeps this one code path working on both
        // providers instead of branching per-provider. Currency-range values lose nothing
        // meaningful at double precision.
        var totalCost = Math.Round((decimal)await filtered.SumAsync(r => (double)r.CostUsd, cancellationToken), 8);

        // OrderByDescending/Take run client-side, after materializing: SQLite's EF provider refuses
        // to translate an ORDER BY on a decimal expression into SQL at all, the same restriction as
        // the Sum(decimal) above. Row counts here are per-guild/per-range user breakdowns, not the
        // full table, so this is a small in-memory sort.
        var rows = await filtered
            .GroupBy(r => r.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                MessageCount = g.Count(),
                InputTokens = g.Sum(r => (long)r.InputTokens),
                OutputTokens = g.Sum(r => (long)r.OutputTokens),
                CachedTokens = g.Sum(r => (long)r.CachedTokens),
                CostUsd = Math.Round((decimal)g.Sum(r => (double)r.CostUsd), 8)
            })
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(r => r.CostUsd)
            .Take(take)
            .Select(r => new LlmUsageByUser
        {
            UserId = r.UserId,
            MessageCount = r.MessageCount,
            InputTokens = r.InputTokens,
            OutputTokens = r.OutputTokens,
            CachedTokens = r.CachedTokens,
            CostUsd = r.CostUsd,
            CostShare = totalCost > 0 ? (double)(r.CostUsd / totalCost) : 0
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmUsageByModel>> GetByModelAsync(LlmUsageQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = Filter(query);

        var rows = await filtered
            .GroupBy(r => r.Model)
            .Select(g => new LlmUsageByModel
            {
                Model = g.Key,
                MessageCount = g.Count(),
                InputTokens = g.Sum(r => (long)r.InputTokens),
                OutputTokens = g.Sum(r => (long)r.OutputTokens),
                CostUsd = Math.Round((decimal)g.Sum(r => (double)r.CostUsd), 8)
            })
            .ToListAsync(cancellationToken);

        // Client-side sort - see the comment on GetByUserAsync's OrderByDescending.
        return rows.OrderByDescending(r => r.CostUsd).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmUsageByMode>> GetByModeAsync(LlmUsageQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = Filter(query);

        var rows = await filtered
            .GroupBy(r => r.Mode)
            .Select(g => new LlmUsageByMode
            {
                Mode = g.Key,
                MessageCount = g.Count(),
                InputTokens = g.Sum(r => (long)r.InputTokens),
                OutputTokens = g.Sum(r => (long)r.OutputTokens),
                CostUsd = Math.Round((decimal)g.Sum(r => (double)r.CostUsd), 8)
            })
            .ToListAsync(cancellationToken);

        // Client-side sort - see the comment on GetByUserAsync's OrderByDescending.
        return rows.OrderByDescending(r => r.CostUsd).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LlmUsageByDay>> GetByDayAsync(LlmUsageQuery query, CancellationToken cancellationToken = default)
    {
        var filtered = Filter(query);

        var rows = await filtered
            .GroupBy(r => r.Timestamp.Date)
            .Select(g => new LlmUsageByDay
            {
                Day = g.Key,
                MessageCount = g.Count(),
                InputTokens = g.Sum(r => (long)r.InputTokens),
                OutputTokens = g.Sum(r => (long)r.OutputTokens),
                CostUsd = Math.Round((decimal)g.Sum(r => (double)r.CostUsd), 8)
            })
            .ToListAsync(cancellationToken);

        return rows.OrderBy(r => r.Day).ToList();
    }

    /// <inheritdoc />
    public async Task<LlmUsagePagedRecords> GetRecordsAsync(LlmUsageQuery query, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var filtered = Filter(query);

        var totalCount = await filtered.CountAsync(cancellationToken);

        var records = await filtered
            .OrderByDescending(r => r.Timestamp)
            .ThenByDescending(r => r.Id)
            .Skip(Math.Max(0, page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new LlmUsagePagedRecords
        {
            Records = records,
            TotalCount = totalCount
        };
    }

    private IQueryable<LlmUsageRecord> Filter(LlmUsageQuery query)
    {
        var filtered = DbSet
            .AsNoTracking()
            .Where(r => r.Timestamp >= query.From && r.Timestamp <= query.To);

        if (query.GuildId.HasValue)
        {
            filtered = filtered.Where(r => r.GuildId == query.GuildId.Value);
        }

        if (query.Mode.HasValue)
        {
            filtered = filtered.Where(r => r.Mode == query.Mode.Value);
        }

        if (query.UserId.HasValue)
        {
            filtered = filtered.Where(r => r.UserId == query.UserId.Value);
        }

        return filtered;
    }
}
