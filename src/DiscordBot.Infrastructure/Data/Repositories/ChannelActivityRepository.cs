using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for ChannelActivitySnapshot entities.
/// </summary>
public class ChannelActivityRepository : Repository<ChannelActivitySnapshot>, IChannelActivityRepository
{
    private readonly ILogger<ChannelActivityRepository> _logger;

    public ChannelActivityRepository(
        BotDbContext context,
        ILogger<ChannelActivityRepository> logger,
        ILogger<Repository<ChannelActivitySnapshot>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<ChannelActivitySnapshot>> GetChannelRankingsAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving top {Limit} channel rankings for guild {GuildId}, {StartDate} to {EndDate}",
            limit, guildId, startDate, endDate);

        // First, get top channel IDs by total messages
        var topChannelIds = await DbSet
            .AsNoTracking()
            .Where(s => s.GuildId == guildId)
            .Where(s => s.PeriodStart >= startDate && s.PeriodStart <= endDate)
            .GroupBy(s => s.ChannelId)
            .Select(g => new
            {
                ChannelId = g.Key,
                TotalMessages = g.Sum(s => s.MessageCount)
            })
            .OrderByDescending(x => x.TotalMessages)
            .Take(limit)
            .Select(x => x.ChannelId)
            .ToListAsync(cancellationToken);

        if (!topChannelIds.Any())
        {
            _logger.LogDebug("No active channels found");
            return Array.Empty<ChannelActivitySnapshot>();
        }

        // Then get all snapshots for those channels in the date range
        var snapshots = await DbSet
            .AsNoTracking()
            .Where(s => s.GuildId == guildId && topChannelIds.Contains(s.ChannelId))
            .Where(s => s.PeriodStart >= startDate && s.PeriodStart <= endDate)
            .ToListAsync(cancellationToken);

        // Group in memory and take the most recent snapshot per channel, ordered by total messages
        var result = snapshots
            .GroupBy(s => s.ChannelId)
            .Select(g => new
            {
                ChannelId = g.Key,
                TotalMessages = g.Sum(s => s.MessageCount),
                MostRecentSnapshot = g.OrderByDescending(s => s.PeriodStart).First()
            })
            .OrderByDescending(x => x.TotalMessages)
            .Select(x => x.MostRecentSnapshot)
            .ToList();

        _logger.LogDebug("Retrieved {Count} channel rankings", result.Count);
        return result;
    }

    public async Task<IReadOnlyList<ChannelActivitySnapshot>> GetChannelTimeSeriesAsync(
        ulong guildId,
        ulong channelId,
        DateTime startDate,
        DateTime endDate,
        SnapshotGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving channel time series for guild {GuildId}, channel {ChannelId}, {StartDate} to {EndDate}, granularity: {Granularity}",
            guildId, channelId, startDate, endDate, granularity);

        var snapshots = await DbSet
            .AsNoTracking()
            .Where(s => s.GuildId == guildId && s.ChannelId == channelId && s.Granularity == granularity)
            .Where(s => s.PeriodStart >= startDate && s.PeriodStart <= endDate)
            .OrderBy(s => s.PeriodStart)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} channel time series snapshots", snapshots.Count);
        return snapshots;
    }

    public async Task UpsertAsync(
        ChannelActivitySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Upserting channel activity snapshot for guild {GuildId}, channel {ChannelId}, period {PeriodStart}",
            snapshot.GuildId, snapshot.ChannelId, snapshot.PeriodStart);

        var existing = await DbSet
            .FirstOrDefaultAsync(
                s => s.GuildId == snapshot.GuildId
                    && s.ChannelId == snapshot.ChannelId
                    && s.PeriodStart == snapshot.PeriodStart
                    && s.Granularity == snapshot.Granularity,
                cancellationToken);

        if (existing != null)
        {
            // Update existing
            existing.ChannelName = snapshot.ChannelName;
            existing.MessageCount = snapshot.MessageCount;
            existing.UniqueUsers = snapshot.UniqueUsers;
            existing.PeakHour = snapshot.PeakHour;
            existing.PeakHourMessageCount = snapshot.PeakHourMessageCount;
            existing.AverageMessageLength = snapshot.AverageMessageLength;
            existing.CreatedAt = DateTime.UtcNow;

            Context.Update(existing);
            _logger.LogDebug("Updated existing channel activity snapshot with ID {Id}", existing.Id);
        }
        else
        {
            // Insert new
            snapshot.CreatedAt = DateTime.UtcNow;
            await DbSet.AddAsync(snapshot, cancellationToken);
            _logger.LogDebug("Inserted new channel activity snapshot");
        }

        await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoff,
        SnapshotGranularity granularity,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Deleting batch of {BatchSize} channel activity snapshots older than {Cutoff}, granularity: {Granularity}",
            batchSize, cutoff, granularity);

        var deletedCount = await DbSet
            .Where(s => s.PeriodStart < cutoff && s.Granularity == granularity)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted {Count} channel activity snapshots (granularity: {Granularity})",
            deletedCount, granularity);

        return deletedCount;
    }
}
