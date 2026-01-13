using DiscordBot.Core.DTOs;
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

    public async Task<bool> HasActiveWatchesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking for any active Rat Watches across all guilds");

        var hasActive = await DbSet
            .AsNoTracking()
            .Where(r => r.Status == RatWatchStatus.Pending || r.Status == RatWatchStatus.Voting)
            .AnyAsync(cancellationToken);

        _logger.LogDebug("Active Rat Watches exist: {HasActive}", hasActive);
        return hasActive;
    }

    public async Task<RatWatchAnalyticsSummaryDto> GetAnalyticsSummaryAsync(
        ulong? guildId,
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving analytics summary for guild {GuildId}, date range {StartDate} to {EndDate}",
            guildId, startDate, endDate);

        var query = DbSet.AsNoTracking();

        // Apply filters
        if (guildId.HasValue)
            query = query.Where(r => r.GuildId == guildId.Value);

        if (startDate.HasValue)
            query = query.Where(r => r.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(r => r.CreatedAt <= endDate.Value);

        var totalWatches = await query.CountAsync(cancellationToken);

        var activeWatches = await query
            .Where(r => r.Status == RatWatchStatus.Pending || r.Status == RatWatchStatus.Voting)
            .CountAsync(cancellationToken);

        var guiltyCount = await query
            .Where(r => r.Status == RatWatchStatus.Guilty)
            .CountAsync(cancellationToken);

        var clearedEarlyCount = await query
            .Where(r => r.Status == RatWatchStatus.ClearedEarly)
            .CountAsync(cancellationToken);

        // Calculate completed watches (either guilty, not guilty, or cleared early)
        var completedWatches = await query
            .Where(r => r.Status == RatWatchStatus.Guilty
                || r.Status == RatWatchStatus.NotGuilty
                || r.Status == RatWatchStatus.ClearedEarly)
            .CountAsync(cancellationToken);

        // Calculate rates (handle division by zero)
        var guiltyRate = completedWatches > 0 ? (double)guiltyCount / completedWatches * 100 : 0;
        var earlyCheckInRate = completedWatches > 0 ? (double)clearedEarlyCount / completedWatches * 100 : 0;

        // Calculate voting participation and margins
        var votingWatches = await query
            .Where(r => r.Status == RatWatchStatus.Guilty || r.Status == RatWatchStatus.NotGuilty)
            .Include(r => r.Votes)
            .ToListAsync(cancellationToken);

        var avgVotingParticipation = votingWatches.Count > 0
            ? votingWatches.Average(r => r.Votes.Count)
            : 0;

        var avgVoteMargin = votingWatches.Count > 0
            ? votingWatches.Average(r => Math.Abs(r.Votes.Count(v => v.IsGuiltyVote) - r.Votes.Count(v => !v.IsGuiltyVote)))
            : 0;

        _logger.LogDebug(
            "Analytics summary: Total={Total}, Active={Active}, Guilty={Guilty}, ClearedEarly={ClearedEarly}",
            totalWatches, activeWatches, guiltyCount, clearedEarlyCount);

        return new RatWatchAnalyticsSummaryDto
        {
            TotalWatches = totalWatches,
            ActiveWatches = activeWatches,
            GuiltyCount = guiltyCount,
            ClearedEarlyCount = clearedEarlyCount,
            GuiltyRate = guiltyRate,
            EarlyCheckInRate = earlyCheckInRate,
            AvgVotingParticipation = avgVotingParticipation,
            AvgVoteMargin = avgVoteMargin
        };
    }

    public async Task<IEnumerable<RatWatchTimeSeriesDto>> GetTimeSeriesAsync(
        ulong? guildId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving time series data for guild {GuildId}, date range {StartDate} to {EndDate}",
            guildId, startDate, endDate);

        var query = DbSet.AsNoTracking();

        // Apply filters
        if (guildId.HasValue)
            query = query.Where(r => r.GuildId == guildId.Value);

        query = query.Where(r => r.CreatedAt >= startDate && r.CreatedAt <= endDate);

        var timeSeries = await query
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new RatWatchTimeSeriesDto
            {
                Date = g.Key,
                TotalCount = g.Count(),
                GuiltyCount = g.Count(r => r.Status == RatWatchStatus.Guilty),
                ClearedCount = g.Count(r => r.Status == RatWatchStatus.ClearedEarly)
            })
            .OrderBy(t => t.Date)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} time series data points", timeSeries.Count);
        return timeSeries;
    }

    public async Task<IEnumerable<ActivityHeatmapDto>> GetActivityHeatmapAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving activity heatmap for guild {GuildId}, date range {StartDate} to {EndDate}",
            guildId, startDate, endDate);

        var heatmap = await DbSet
            .AsNoTracking()
            .Where(r => r.GuildId == guildId
                && r.ScheduledAt >= startDate
                && r.ScheduledAt <= endDate)
            .GroupBy(r => new
            {
                DayOfWeek = (int)r.ScheduledAt.DayOfWeek,
                Hour = r.ScheduledAt.Hour
            })
            .Select(g => new ActivityHeatmapDto
            {
                DayOfWeek = g.Key.DayOfWeek,
                Hour = g.Key.Hour,
                Count = g.Count()
            })
            .OrderBy(h => h.DayOfWeek)
            .ThenBy(h => h.Hour)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} heatmap data points", heatmap.Count);
        return heatmap;
    }

    public async Task<(IEnumerable<RatWatch> Items, int TotalCount)> GetFilteredByGuildAsync(
        ulong guildId,
        RatWatchIncidentFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving filtered Rat Watches for guild {GuildId} with filters: Statuses={Statuses}, StartDate={StartDate}, EndDate={EndDate}, AccusedUser={AccusedUser}, InitiatorUser={InitiatorUser}, MinVoteCount={MinVoteCount}, Keyword={Keyword}, Page={Page}, PageSize={PageSize}, SortBy={SortBy}, SortDescending={SortDescending}",
            guildId,
            filter.Statuses != null ? string.Join(",", filter.Statuses) : "null",
            filter.StartDate,
            filter.EndDate,
            filter.AccusedUser,
            filter.InitiatorUser,
            filter.MinVoteCount,
            filter.Keyword,
            filter.Page,
            filter.PageSize,
            filter.SortBy,
            filter.SortDescending);

        var query = DbSet
            .AsNoTracking()
            .Include(r => r.Guild)
            .Include(r => r.Votes)
            .Where(r => r.GuildId == guildId);

        // Apply status filter
        if (filter.Statuses != null && filter.Statuses.Count > 0)
        {
            query = query.Where(r => filter.Statuses.Contains(r.Status));
        }

        // Apply date range filters
        if (filter.StartDate.HasValue)
        {
            query = query.Where(r => r.ScheduledAt >= filter.StartDate.Value);
        }

        if (filter.EndDate.HasValue)
        {
            query = query.Where(r => r.ScheduledAt <= filter.EndDate.Value);
        }

        // Apply accused user filter (by Discord ID only - username filtering happens at service layer)
        if (!string.IsNullOrWhiteSpace(filter.AccusedUser))
        {
            if (ulong.TryParse(filter.AccusedUser, out var accusedUserId))
            {
                query = query.Where(r => r.AccusedUserId == accusedUserId);
            }
        }

        // Apply initiator user filter (by Discord ID only - username filtering happens at service layer)
        if (!string.IsNullOrWhiteSpace(filter.InitiatorUser))
        {
            if (ulong.TryParse(filter.InitiatorUser, out var initiatorUserId))
            {
                query = query.Where(r => r.InitiatorUserId == initiatorUserId);
            }
        }

        // Apply minimum vote count filter
        if (filter.MinVoteCount.HasValue)
        {
            query = query.Where(r => r.Votes.Count >= filter.MinVoteCount.Value);
        }

        // Apply keyword search in custom message
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.ToLower();
            query = query.Where(r => r.CustomMessage != null && r.CustomMessage.ToLower().Contains(keyword));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply sorting
        query = filter.SortBy.ToLowerInvariant() switch
        {
            "scheduledat" => filter.SortDescending
                ? query.OrderByDescending(r => r.ScheduledAt)
                : query.OrderBy(r => r.ScheduledAt),
            "createdat" => filter.SortDescending
                ? query.OrderByDescending(r => r.CreatedAt)
                : query.OrderBy(r => r.CreatedAt),
            "status" => filter.SortDescending
                ? query.OrderByDescending(r => r.Status)
                : query.OrderBy(r => r.Status),
            "accuseduser" => filter.SortDescending
                ? query.OrderByDescending(r => r.AccusedUserId)
                : query.OrderBy(r => r.AccusedUserId),
            "initiatoruser" => filter.SortDescending
                ? query.OrderByDescending(r => r.InitiatorUserId)
                : query.OrderBy(r => r.InitiatorUserId),
            _ => filter.SortDescending
                ? query.OrderByDescending(r => r.ScheduledAt)
                : query.OrderBy(r => r.ScheduledAt)
        };

        // Apply pagination
        var skip = (filter.Page - 1) * filter.PageSize;
        var items = await query
            .Skip(skip)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} filtered Rat Watches for guild {GuildId} out of {TotalCount} total",
            items.Count, guildId, totalCount);

        return (items, totalCount);
    }

    public async Task<IEnumerable<RatWatch>> GetRecentAsync(int limit, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving {Limit} most recent Rat Watches across all guilds", limit);

        // Query all watches and compute the activity timestamp based on status
        // Activity timestamp logic:
        // - Pending: CreatedAt
        // - Voting: VotingStartedAt
        // - Guilty/NotGuilty: VotingEndedAt
        // - ClearedEarly: ClearedAt
        // - Cancelled: CreatedAt (fallback)
        var watches = await DbSet
            .AsNoTracking()
            .Include(r => r.Guild)
            .OrderByDescending(r =>
                r.Status == RatWatchStatus.ClearedEarly && r.ClearedAt.HasValue ? r.ClearedAt.Value :
                (r.Status == RatWatchStatus.Guilty || r.Status == RatWatchStatus.NotGuilty) && r.VotingEndedAt.HasValue ? r.VotingEndedAt.Value :
                r.Status == RatWatchStatus.Voting && r.VotingStartedAt.HasValue ? r.VotingStartedAt.Value :
                r.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} recent Rat Watches", watches.Count);
        return watches;
    }

    public async Task<bool> HasStatusAsync(Guid watchId, RatWatchStatus expectedStatus, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking if Rat Watch {WatchId} has status {ExpectedStatus}", watchId, expectedStatus);

        var hasStatus = await DbSet
            .AsNoTracking()
            .Where(r => r.Id == watchId && r.Status == expectedStatus)
            .AnyAsync(cancellationToken);

        _logger.LogDebug("Rat Watch {WatchId} has status {ExpectedStatus}: {Result}", watchId, expectedStatus, hasStatus);
        return hasStatus;
    }
}
