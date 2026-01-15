using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for AssistantUsageMetrics entities.
/// Provides data access operations for assistant usage tracking, token consumption, and cost analytics.
/// </summary>
public class AssistantUsageMetricsRepository : Repository<AssistantUsageMetrics>, IAssistantUsageMetricsRepository
{
    private readonly ILogger<AssistantUsageMetricsRepository> _logger;

    public AssistantUsageMetricsRepository(
        BotDbContext context,
        ILogger<AssistantUsageMetricsRepository> logger,
        ILogger<Repository<AssistantUsageMetrics>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AssistantUsageMetrics> GetOrCreateAsync(
        ulong guildId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        // Normalize to date-only (remove time component)
        var dateOnly = date.Date;

        _logger.LogDebug(
            "Getting or creating assistant usage metrics for guild {GuildId} on {Date}",
            guildId, dateOnly);

        var metrics = await DbSet
            .FirstOrDefaultAsync(m => m.GuildId == guildId && m.Date == dateOnly, cancellationToken);

        if (metrics != null)
        {
            _logger.LogDebug("Existing metrics found for guild {GuildId} on {Date}", guildId, dateOnly);
            return metrics;
        }

        _logger.LogInformation("Creating new metrics record for guild {GuildId} on {Date}", guildId, dateOnly);

        var now = DateTime.UtcNow;
        metrics = new AssistantUsageMetrics
        {
            GuildId = guildId,
            Date = dateOnly,
            TotalQuestions = 0,
            TotalInputTokens = 0,
            TotalOutputTokens = 0,
            TotalCachedTokens = 0,
            TotalCacheWriteTokens = 0,
            TotalCacheHits = 0,
            TotalCacheMisses = 0,
            TotalToolCalls = 0,
            EstimatedCostUsd = 0m,
            FailedRequests = 0,
            AverageLatencyMs = 0,
            UpdatedAt = now
        };

        await DbSet.AddAsync(metrics, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created new metrics record for guild {GuildId} on {Date} with ID {Id}",
            guildId, dateOnly, metrics.Id);

        return metrics;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AssistantUsageMetrics>> GetRangeAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // Normalize to date-only
        var startDateOnly = startDate.Date;
        var endDateOnly = endDate.Date;

        _logger.LogDebug(
            "Retrieving metrics for guild {GuildId} from {StartDate} to {EndDate}",
            guildId, startDateOnly, endDateOnly);

        var metrics = await DbSet
            .AsNoTracking()
            .Where(m => m.GuildId == guildId &&
                       m.Date >= startDateOnly &&
                       m.Date <= endDateOnly)
            .OrderBy(m => m.Date)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} metrics records for guild {GuildId}",
            metrics.Count, guildId);

        return metrics;
    }

    /// <inheritdoc />
    public async Task IncrementMetricsAsync(
        ulong guildId,
        DateTime date,
        int inputTokens,
        int outputTokens,
        int cachedTokens,
        int cacheWriteTokens,
        bool cacheHit,
        int toolCalls,
        int latencyMs,
        decimal cost,
        CancellationToken cancellationToken = default)
    {
        var metrics = await GetOrCreateAsync(guildId, date, cancellationToken);

        _logger.LogDebug(
            "Incrementing metrics for guild {GuildId} on {Date}: +{InputTokens} input, +{OutputTokens} output, +{CachedTokens} cached",
            guildId, date.Date, inputTokens, outputTokens, cachedTokens);

        // Update metrics
        metrics.TotalQuestions++;
        metrics.TotalInputTokens += inputTokens;
        metrics.TotalOutputTokens += outputTokens;
        metrics.TotalCachedTokens += cachedTokens;
        metrics.TotalCacheWriteTokens += cacheWriteTokens;
        metrics.TotalToolCalls += toolCalls;
        metrics.EstimatedCostUsd += cost;

        if (cacheHit)
        {
            metrics.TotalCacheHits++;
        }
        else
        {
            metrics.TotalCacheMisses++;
        }

        // Calculate running average for latency
        var totalRequests = metrics.TotalQuestions;
        metrics.AverageLatencyMs = ((metrics.AverageLatencyMs * (totalRequests - 1)) + latencyMs) / totalRequests;

        metrics.UpdatedAt = DateTime.UtcNow;
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Updated metrics for guild {GuildId}: TotalQuestions={TotalQuestions}, TotalCost={TotalCost:C}",
            guildId, metrics.TotalQuestions, metrics.EstimatedCostUsd);
    }

    /// <inheritdoc />
    public async Task IncrementFailedRequestAsync(
        ulong guildId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var metrics = await GetOrCreateAsync(guildId, date, cancellationToken);

        metrics.FailedRequests++;
        metrics.UpdatedAt = DateTime.UtcNow;

        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Incremented failed request count for guild {GuildId} on {Date}. Total failures: {FailedRequests}",
            guildId, date.Date, metrics.FailedRequests);
    }

    /// <inheritdoc />
    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate,
        CancellationToken cancellationToken = default)
    {
        var cutoffDateOnly = cutoffDate.Date;

        _logger.LogInformation(
            "Deleting assistant usage metrics older than {CutoffDate}",
            cutoffDateOnly);

        var deletedCount = await DbSet
            .Where(m => m.Date < cutoffDateOnly)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted {Count} assistant usage metrics records older than {CutoffDate}",
            deletedCount, cutoffDateOnly);

        return deletedCount;
    }

    /// <summary>
    /// Gets aggregated metrics across all guilds for a date range.
    /// </summary>
    /// <param name="startDate">Start date (inclusive, UTC).</param>
    /// <param name="endDate">End date (inclusive, UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated metrics.</returns>
    public async Task<AggregatedUsageMetrics> GetAggregatedMetricsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var startDateOnly = startDate.Date;
        var endDateOnly = endDate.Date;

        _logger.LogDebug(
            "Aggregating metrics from {StartDate} to {EndDate}",
            startDateOnly, endDateOnly);

        var result = await DbSet
            .AsNoTracking()
            .Where(m => m.Date >= startDateOnly && m.Date <= endDateOnly)
            .GroupBy(m => 1) // Group all into single result
            .Select(g => new AggregatedUsageMetrics
            {
                TotalGuilds = g.Select(m => m.GuildId).Distinct().Count(),
                TotalQuestions = g.Sum(m => m.TotalQuestions),
                TotalInputTokens = g.Sum(m => m.TotalInputTokens),
                TotalOutputTokens = g.Sum(m => m.TotalOutputTokens),
                TotalCachedTokens = g.Sum(m => m.TotalCachedTokens),
                TotalToolCalls = g.Sum(m => m.TotalToolCalls),
                TotalCost = g.Sum(m => m.EstimatedCostUsd),
                TotalFailedRequests = g.Sum(m => m.FailedRequests),
                CacheHitRate = g.Sum(m => m.TotalCacheHits + m.TotalCacheMisses) > 0
                    ? (double)g.Sum(m => m.TotalCacheHits) / g.Sum(m => m.TotalCacheHits + m.TotalCacheMisses)
                    : 0
            })
            .FirstOrDefaultAsync(cancellationToken);

        return result ?? new AggregatedUsageMetrics();
    }
}

/// <summary>
/// Aggregated usage metrics for reporting.
/// </summary>
public class AggregatedUsageMetrics
{
    public int TotalGuilds { get; set; }
    public int TotalQuestions { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public int TotalCachedTokens { get; set; }
    public int TotalToolCalls { get; set; }
    public decimal TotalCost { get; set; }
    public int TotalFailedRequests { get; set; }
    public double CacheHitRate { get; set; }
}
