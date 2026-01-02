using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for MetricSnapshot entities with time-series data operations.
/// </summary>
public class MetricSnapshotRepository : IMetricSnapshotRepository
{
    private readonly BotDbContext _context;
    private readonly ILogger<MetricSnapshotRepository> _logger;

    public MetricSnapshotRepository(BotDbContext context, ILogger<MetricSnapshotRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AddAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _logger.LogTrace(
            "Adding metric snapshot: Timestamp={Timestamp}, WorkingSetMB={WorkingSetMB}, AvgQueryTimeMs={AvgQueryTimeMs}",
            snapshot.Timestamp,
            snapshot.WorkingSetMB,
            snapshot.DatabaseAvgQueryTimeMs);

        await _context.MetricSnapshots.AddAsync(snapshot, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Metric snapshot added with ID {SnapshotId}", snapshot.Id);
    }

    public async Task<IReadOnlyList<MetricSnapshotDto>> GetRangeAsync(
        DateTime startTime,
        DateTime endTime,
        int aggregationMinutes = 0,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving metric snapshots from {StartTime} to {EndTime} with aggregation {AggregationMinutes} minutes",
            startTime,
            endTime,
            aggregationMinutes);

        // Ensure times are in UTC
        startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
        endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);

        var query = _context.MetricSnapshots
            .AsNoTracking()
            .Where(m => m.Timestamp >= startTime && m.Timestamp <= endTime);

        if (aggregationMinutes <= 0)
        {
            // Return raw samples
            var rawSamples = await query
                .OrderBy(m => m.Timestamp)
                .Select(m => new MetricSnapshotDto
                {
                    Timestamp = m.Timestamp,
                    DatabaseAvgQueryTimeMs = m.DatabaseAvgQueryTimeMs,
                    WorkingSetMB = m.WorkingSetMB,
                    HeapSizeMB = m.HeapSizeMB,
                    CacheHitRatePercent = m.CacheHitRatePercent,
                    ServicesRunningCount = m.ServicesRunningCount,
                    Gen0Collections = m.Gen0Collections,
                    Gen1Collections = m.Gen1Collections,
                    Gen2Collections = m.Gen2Collections
                })
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Retrieved {Count} raw metric snapshots", rawSamples.Count);
            return rawSamples;
        }
        else
        {
            // Aggregate into time buckets
            var samples = await query
                .OrderBy(m => m.Timestamp)
                .ToListAsync(cancellationToken);

            if (samples.Count == 0)
            {
                _logger.LogInformation("No metric snapshots found in specified range");
                return Array.Empty<MetricSnapshotDto>();
            }

            // Group by time bucket
            var bucketSize = TimeSpan.FromMinutes(aggregationMinutes);
            var aggregated = samples
                .GroupBy(m =>
                {
                    // Calculate bucket start time
                    var ticks = m.Timestamp.Ticks;
                    var bucketTicks = ticks / bucketSize.Ticks * bucketSize.Ticks;
                    return new DateTime(bucketTicks, DateTimeKind.Utc);
                })
                .Select(g => new MetricSnapshotDto
                {
                    Timestamp = g.Key,
                    DatabaseAvgQueryTimeMs = g.Average(m => m.DatabaseAvgQueryTimeMs),
                    WorkingSetMB = (long)g.Average(m => m.WorkingSetMB),
                    HeapSizeMB = (long)g.Average(m => m.HeapSizeMB),
                    CacheHitRatePercent = g.Average(m => m.CacheHitRatePercent),
                    ServicesRunningCount = (int)g.Average(m => m.ServicesRunningCount),
                    Gen0Collections = (int)g.Average(m => m.Gen0Collections),
                    Gen1Collections = (int)g.Average(m => m.Gen1Collections),
                    Gen2Collections = (int)g.Average(m => m.Gen2Collections)
                })
                .OrderBy(m => m.Timestamp)
                .ToList();

            _logger.LogInformation(
                "Aggregated {SampleCount} metric snapshots into {BucketCount} buckets of {AggregationMinutes} minutes",
                samples.Count,
                aggregated.Count,
                aggregationMinutes);

            return aggregated;
        }
    }

    public async Task<MetricSnapshot?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving latest metric snapshot");

        var latest = await _context.MetricSnapshots
            .AsNoTracking()
            .OrderByDescending(m => m.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest != null)
        {
            _logger.LogDebug("Latest metric snapshot: Timestamp={Timestamp}, ID={Id}", latest.Timestamp, latest.Id);
        }
        else
        {
            _logger.LogDebug("No metric snapshots found");
        }

        return latest;
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting metric snapshots older than {CutoffDate}", cutoffDate);

        // Ensure cutoff is in UTC
        cutoffDate = DateTime.SpecifyKind(cutoffDate, DateTimeKind.Utc);

        var toDelete = await _context.MetricSnapshots
            .Where(m => m.Timestamp < cutoffDate)
            .ToListAsync(cancellationToken);

        var count = toDelete.Count;

        if (count > 0)
        {
            _context.MetricSnapshots.RemoveRange(toDelete);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted {Count} metric snapshots older than {CutoffDate}", count, cutoffDate);
        }
        else
        {
            _logger.LogDebug("No metric snapshots found older than {CutoffDate}", cutoffDate);
        }

        return count;
    }

    public async Task<int> GetCountAsync(DateTime startTime, DateTime endTime, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Counting metric snapshots between {StartTime} and {EndTime}", startTime, endTime);

        // Ensure times are in UTC
        startTime = DateTime.SpecifyKind(startTime, DateTimeKind.Utc);
        endTime = DateTime.SpecifyKind(endTime, DateTimeKind.Utc);

        var count = await _context.MetricSnapshots
            .AsNoTracking()
            .Where(m => m.Timestamp >= startTime && m.Timestamp <= endTime)
            .CountAsync(cancellationToken);

        _logger.LogDebug("Found {Count} metric snapshots in specified range", count);

        return count;
    }
}
