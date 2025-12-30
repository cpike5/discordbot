using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for RatWatch entities with rat-watch-specific operations.
/// </summary>
public class RatWatchRepository : Repository<RatWatch>, IRatWatchRepository
{
    private readonly ILogger<RatWatchRepository> _logger;

    public RatWatchRepository(
        BotDbContext context,
        ILogger<RatWatchRepository> logger,
        ILogger<Repository<RatWatch>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Overrides base implementation to include Guild navigation property.
    /// </remarks>
    public override async Task<RatWatch?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving Rat Watch by ID: {Id}", id);

        if (id is not Guid guidId)
        {
            _logger.LogWarning("Invalid ID type for RatWatch: {IdType}", id?.GetType().Name ?? "null");
            return null;
        }

        var result = await DbSet
            .AsNoTracking()
            .Include(r => r.Guild)
            .FirstOrDefaultAsync(r => r.Id == guidId, cancellationToken);

        _logger.LogDebug("Rat Watch {Id} found: {Found}", id, result != null);
        return result;
    }

    public async Task<IEnumerable<RatWatch>> GetPendingWatchesAsync(DateTime beforeTime, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving pending Rat Watches due before {BeforeTime}", beforeTime);

        var watches = await DbSet
            .AsNoTracking()
            .Include(r => r.Guild)
            .Where(r => r.Status == RatWatchStatus.Pending && r.ScheduledAt <= beforeTime)
            .OrderBy(r => r.ScheduledAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} pending Rat Watches due for execution", watches.Count);
        return watches;
    }

    public async Task<IEnumerable<RatWatch>> GetActiveVotingAsync(DateTime votingEndBefore, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving active voting Rat Watches with voting ending before {VotingEndBefore}", votingEndBefore);

        var watches = await DbSet
            .AsNoTracking()
            .Include(r => r.Guild)
            .Include(r => r.Votes)
            .Where(r => r.Status == RatWatchStatus.Voting && r.VotingStartedAt.HasValue && r.VotingStartedAt.Value <= votingEndBefore)
            .OrderBy(r => r.VotingStartedAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} voting Rat Watches needing finalization", watches.Count);
        return watches;
    }

    public async Task<RatWatch?> GetByIdWithVotesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving Rat Watch {Id} with votes", id);

        var result = await DbSet
            .AsNoTracking()
            .Include(r => r.Guild)
            .Include(r => r.Votes)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        _logger.LogDebug("Rat Watch {Id} found: {Found}, Votes: {VoteCount}",
            id, result != null, result?.Votes.Count ?? 0);
        return result;
    }

    public async Task<(IEnumerable<RatWatch> Items, int TotalCount)> GetByGuildAsync(
        ulong guildId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving Rat Watches for guild {GuildId}, page {Page}, pageSize {PageSize}",
            guildId, page, pageSize);

        var query = DbSet
            .AsNoTracking()
            .Include(r => r.Guild)
            .Where(r => r.GuildId == guildId);

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (page - 1) * pageSize;
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} Rat Watches for guild {GuildId} out of {TotalCount} total",
            items.Count, guildId, totalCount);

        return (items, totalCount);
    }

    public async Task<RatWatch?> FindDuplicateAsync(
        ulong guildId,
        ulong accusedUserId,
        DateTime scheduledAt,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Checking for duplicate Rat Watch in guild {GuildId} for user {UserId} at {ScheduledAt}",
            guildId, accusedUserId, scheduledAt);

        // Consider watches within 5 minutes of the scheduled time as duplicates
        var timeWindow = TimeSpan.FromMinutes(5);
        var startTime = scheduledAt.Subtract(timeWindow);
        var endTime = scheduledAt.Add(timeWindow);

        var duplicate = await DbSet
            .AsNoTracking()
            .Where(r => r.GuildId == guildId
                && r.AccusedUserId == accusedUserId
                && r.ScheduledAt >= startTime
                && r.ScheduledAt <= endTime
                && (r.Status == RatWatchStatus.Pending || r.Status == RatWatchStatus.Voting))
            .FirstOrDefaultAsync(cancellationToken);

        _logger.LogDebug("Duplicate check result: {Found}", duplicate != null);
        return duplicate;
    }

    public async Task<int> GetActiveWatchCountForUserAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Counting active Rat Watches for user {UserId} in guild {GuildId}",
            userId, guildId);

        var count = await DbSet
            .AsNoTracking()
            .Where(r => r.GuildId == guildId
                && r.AccusedUserId == userId
                && (r.Status == RatWatchStatus.Pending || r.Status == RatWatchStatus.Voting))
            .CountAsync(cancellationToken);

        _logger.LogDebug("User {UserId} has {Count} active Rat Watches", userId, count);
        return count;
    }
}
