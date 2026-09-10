using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository for the <see cref="LlmUsageRecord"/> ledger — one row per user message across every
/// <c>LlmMode</c>. Grouped queries back the usage dashboard and per-user cost breakdowns; retention
/// and per-user purge/export live on top of <see cref="DeleteOlderThanAsync"/> and
/// <see cref="DeleteByUserAsync"/> in other services, not here.
/// </summary>
public interface ILlmUsageRepository : IRepository<LlmUsageRecord>
{
    /// <summary>Bulk-inserts a batch of records (the write path used by the background queue processor).</summary>
    Task AddRangeAsync(IReadOnlyCollection<LlmUsageRecord> records, CancellationToken cancellationToken = default);

    /// <summary>Deletes rows with <c>Timestamp &lt; cutoff</c>, in batches of at most <paramref name="batchSize"/> per round-trip.</summary>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Deletes every row for <paramref name="userId"/> (GDPR purge).</summary>
    Task<int> DeleteByUserAsync(ulong userId, CancellationToken cancellationToken = default);

    /// <summary>Counts rows for <paramref name="userId"/> (used by data export to size the response).</summary>
    Task<int> CountByUserAsync(ulong userId, CancellationToken cancellationToken = default);

    /// <summary>Aggregate totals (messages, tokens, cost, billed share, failures, avg latency) over <paramref name="query"/>.</summary>
    Task<LlmUsageTotals> GetTotalsAsync(LlmUsageQuery query, CancellationToken cancellationToken = default);

    /// <summary>Per-user breakdown over <paramref name="query"/>, ordered by cost descending.</summary>
    Task<IReadOnlyList<LlmUsageByUser>> GetByUserAsync(LlmUsageQuery query, int take, CancellationToken cancellationToken = default);

    /// <summary>Per-model breakdown over <paramref name="query"/>.</summary>
    Task<IReadOnlyList<LlmUsageByModel>> GetByModelAsync(LlmUsageQuery query, CancellationToken cancellationToken = default);

    /// <summary>Per-mode breakdown over <paramref name="query"/>.</summary>
    Task<IReadOnlyList<LlmUsageByMode>> GetByModeAsync(LlmUsageQuery query, CancellationToken cancellationToken = default);

    /// <summary>Per-day breakdown (UTC calendar day) over <paramref name="query"/>.</summary>
    Task<IReadOnlyList<LlmUsageByDay>> GetByDayAsync(LlmUsageQuery query, CancellationToken cancellationToken = default);

    /// <summary>Paged raw rows over <paramref name="query"/>, newest first.</summary>
    Task<LlmUsagePagedRecords> GetRecordsAsync(LlmUsageQuery query, int page, int pageSize, CancellationToken cancellationToken = default);
}
