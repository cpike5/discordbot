using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for MemberActivitySnapshot entities.
/// </summary>
public class MemberActivityRepository : Repository<MemberActivitySnapshot>, IMemberActivityRepository
{
    private readonly ILogger<MemberActivityRepository> _logger;

    public MemberActivityRepository(
        BotDbContext context,
        ILogger<MemberActivityRepository> logger,
        ILogger<Repository<MemberActivitySnapshot>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<MemberActivitySnapshot>> GetByMemberAsync(
        ulong guildId,
        ulong userId,
        DateTime startDate,
        DateTime endDate,
        SnapshotGranularity? granularity = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving member activity for guild {GuildId}, user {UserId}, {StartDate} to {EndDate}, granularity: {Granularity}",
            guildId, userId, startDate, endDate, granularity);

        var query = DbSet
            .AsNoTracking()
            .Where(s => s.GuildId == guildId && s.UserId == userId)
            .Where(s => s.PeriodStart >= startDate && s.PeriodStart <= endDate);

        if (granularity.HasValue)
        {
            query = query.Where(s => s.Granularity == granularity.Value);
        }

        var snapshots = await query
            .OrderBy(s => s.PeriodStart)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} member activity snapshots", snapshots.Count);
        return snapshots;
    }

    public async Task<IReadOnlyList<MemberActivitySnapshot>> GetTopActiveMembersAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving top {Limit} active members for guild {GuildId}, {StartDate} to {EndDate}",
            limit, guildId, startDate, endDate);

        // First, get aggregated totals per user
        var topUserIds = await DbSet
            .AsNoTracking()
            .Where(s => s.GuildId == guildId)
            .Where(s => s.PeriodStart >= startDate && s.PeriodStart <= endDate)
            .GroupBy(s => s.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalMessages = g.Sum(s => s.MessageCount)
            })
            .OrderByDescending(x => x.TotalMessages)
            .Take(limit)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);

        if (!topUserIds.Any())
        {
            _logger.LogDebug("No active members found");
            return Array.Empty<MemberActivitySnapshot>();
        }

        // Then get the most recent snapshot for each top user
        var snapshots = await DbSet
            .AsNoTracking()
            .Include(s => s.User)
            .Where(s => s.GuildId == guildId && topUserIds.Contains(s.UserId))
            .Where(s => s.PeriodStart >= startDate && s.PeriodStart <= endDate)
            .ToListAsync(cancellationToken);

        // Group in memory and take the most recent snapshot per user, ordered by total messages
        var result = snapshots
            .GroupBy(s => s.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalMessages = g.Sum(s => s.MessageCount),
                MostRecentSnapshot = g.OrderByDescending(s => s.PeriodStart).First()
            })
            .OrderByDescending(x => x.TotalMessages)
            .Select(x => x.MostRecentSnapshot)
            .ToList();

        _logger.LogDebug("Retrieved {Count} top active members", result.Count);
        return result;
    }

    public async Task<IReadOnlyList<(DateTime Period, int TotalMessages, int ActiveMembers)>> GetActivityTimeSeriesAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        SnapshotGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving activity time series for guild {GuildId}, {StartDate} to {EndDate}, granularity: {Granularity}",
            guildId, startDate, endDate, granularity);

        var results = await DbSet
            .AsNoTracking()
            .Where(s => s.GuildId == guildId && s.Granularity == granularity)
            .Where(s => s.PeriodStart >= startDate && s.PeriodStart <= endDate)
            .GroupBy(s => s.PeriodStart)
            .Select(g => new
            {
                Period = g.Key,
                TotalMessages = g.Sum(s => s.MessageCount),
                ActiveMembers = g.Count()
            })
            .OrderBy(x => x.Period)
            .ToListAsync(cancellationToken);

        var tuples = results.Select(r => (r.Period, r.TotalMessages, r.ActiveMembers)).ToList();

        _logger.LogDebug("Retrieved {Count} time series data points", tuples.Count);
        return tuples;
    }

    public async Task UpsertAsync(
        MemberActivitySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Upserting member activity snapshot for guild {GuildId}, user {UserId}, period {PeriodStart}",
            snapshot.GuildId, snapshot.UserId, snapshot.PeriodStart);

        var existing = await DbSet
            .FirstOrDefaultAsync(
                s => s.GuildId == snapshot.GuildId
                    && s.UserId == snapshot.UserId
                    && s.PeriodStart == snapshot.PeriodStart
                    && s.Granularity == snapshot.Granularity,
                cancellationToken);

        if (existing != null)
        {
            // Update existing
            existing.MessageCount = snapshot.MessageCount;
            existing.ReactionCount = snapshot.ReactionCount;
            existing.VoiceMinutes = snapshot.VoiceMinutes;
            existing.UniqueChannelsActive = snapshot.UniqueChannelsActive;
            existing.CreatedAt = DateTime.UtcNow;

            Context.Update(existing);
            _logger.LogDebug("Updated existing member activity snapshot with ID {Id}", existing.Id);
        }
        else
        {
            // Insert new
            snapshot.CreatedAt = DateTime.UtcNow;
            await DbSet.AddAsync(snapshot, cancellationToken);
            _logger.LogDebug("Inserted new member activity snapshot");
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
            "Deleting batch of {BatchSize} member activity snapshots older than {Cutoff}, granularity: {Granularity}",
            batchSize, cutoff, granularity);

        var deletedCount = await DbSet
            .Where(s => s.PeriodStart < cutoff && s.Granularity == granularity)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted {Count} member activity snapshots (granularity: {Granularity})",
            deletedCount, granularity);

        return deletedCount;
    }

    public async Task<DateTime?> GetLastSnapshotTimeAsync(
        ulong guildId,
        SnapshotGranularity granularity,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving last snapshot time for guild {GuildId}, granularity: {Granularity}",
            guildId, granularity);

        var lastTime = await DbSet
            .AsNoTracking()
            .Where(s => s.GuildId == guildId && s.Granularity == granularity)
            .OrderByDescending(s => s.PeriodStart)
            .Select(s => s.PeriodStart)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastTime == default)
        {
            _logger.LogDebug("No snapshots found");
            return null;
        }

        _logger.LogDebug("Last snapshot time: {LastTime}", lastTime);
        return lastTime;
    }
}
