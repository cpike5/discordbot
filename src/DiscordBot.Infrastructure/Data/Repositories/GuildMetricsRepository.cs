using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for GuildMetricsSnapshot entities.
/// </summary>
public class GuildMetricsRepository : Repository<GuildMetricsSnapshot>, IGuildMetricsRepository
{
    private readonly ILogger<GuildMetricsRepository> _logger;

    public GuildMetricsRepository(
        BotDbContext context,
        ILogger<GuildMetricsRepository> logger,
        ILogger<Repository<GuildMetricsSnapshot>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<GuildMetricsSnapshot>> GetByDateRangeAsync(
        ulong guildId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving guild metrics for guild {GuildId}, {StartDate} to {EndDate}",
            guildId, startDate, endDate);

        var snapshots = await DbSet
            .AsNoTracking()
            .Where(s => s.GuildId == guildId)
            .Where(s => s.SnapshotDate >= startDate && s.SnapshotDate <= endDate)
            .OrderBy(s => s.SnapshotDate)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} guild metrics snapshots", snapshots.Count);
        return snapshots;
    }

    public async Task<GuildMetricsSnapshot?> GetLatestAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving latest guild metrics for guild {GuildId}", guildId);

        var snapshot = await DbSet
            .AsNoTracking()
            .Where(s => s.GuildId == guildId)
            .OrderByDescending(s => s.SnapshotDate)
            .FirstOrDefaultAsync(cancellationToken);

        _logger.LogDebug("Latest snapshot found: {Found}", snapshot != null);
        return snapshot;
    }

    public async Task<IReadOnlyList<(DateOnly Date, int NetGrowth, int Joined, int Left)>> GetGrowthTimeSeriesAsync(
        ulong guildId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving growth time series for guild {GuildId}, {StartDate} to {EndDate}",
            guildId, startDate, endDate);

        var results = await DbSet
            .AsNoTracking()
            .Where(s => s.GuildId == guildId)
            .Where(s => s.SnapshotDate >= startDate && s.SnapshotDate <= endDate)
            .OrderBy(s => s.SnapshotDate)
            .Select(s => new
            {
                Date = s.SnapshotDate,
                NetGrowth = s.MembersJoined - s.MembersLeft,
                Joined = s.MembersJoined,
                Left = s.MembersLeft
            })
            .ToListAsync(cancellationToken);

        var tuples = results.Select(r => (r.Date, r.NetGrowth, r.Joined, r.Left)).ToList();

        _logger.LogDebug("Retrieved {Count} growth data points", tuples.Count);
        return tuples;
    }

    public async Task UpsertAsync(
        GuildMetricsSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Upserting guild metrics snapshot for guild {GuildId}, date {SnapshotDate}",
            snapshot.GuildId, snapshot.SnapshotDate);

        var existing = await DbSet
            .FirstOrDefaultAsync(
                s => s.GuildId == snapshot.GuildId && s.SnapshotDate == snapshot.SnapshotDate,
                cancellationToken);

        if (existing != null)
        {
            // Update existing
            existing.TotalMembers = snapshot.TotalMembers;
            existing.ActiveMembers = snapshot.ActiveMembers;
            existing.MembersJoined = snapshot.MembersJoined;
            existing.MembersLeft = snapshot.MembersLeft;
            existing.TotalMessages = snapshot.TotalMessages;
            existing.CommandsExecuted = snapshot.CommandsExecuted;
            existing.ModerationActions = snapshot.ModerationActions;
            existing.ActiveChannels = snapshot.ActiveChannels;
            existing.TotalVoiceMinutes = snapshot.TotalVoiceMinutes;
            existing.CreatedAt = DateTime.UtcNow;

            Context.Update(existing);
            _logger.LogDebug("Updated existing guild metrics snapshot with ID {Id}", existing.Id);
        }
        else
        {
            // Insert new
            snapshot.CreatedAt = DateTime.UtcNow;
            await DbSet.AddAsync(snapshot, cancellationToken);
            _logger.LogDebug("Inserted new guild metrics snapshot");
        }

        await Context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteOlderThanAsync(
        DateOnly cutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Deleting batch of {BatchSize} guild metrics snapshots older than {Cutoff}",
            batchSize, cutoff);

        var deletedCount = await DbSet
            .Where(s => s.SnapshotDate < cutoff)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} guild metrics snapshots", deletedCount);

        return deletedCount;
    }
}
