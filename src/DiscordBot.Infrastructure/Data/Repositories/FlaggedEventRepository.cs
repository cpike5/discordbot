using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for FlaggedEvent entities with auto-moderation-specific operations.
/// </summary>
public class FlaggedEventRepository : Repository<FlaggedEvent>, IFlaggedEventRepository
{
    private readonly ILogger<FlaggedEventRepository> _logger;

    public FlaggedEventRepository(
        BotDbContext context,
        ILogger<FlaggedEventRepository> logger,
        ILogger<Repository<FlaggedEvent>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Overrides base implementation to include Guild navigation property.
    /// </remarks>
    public override async Task<FlaggedEvent?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving flagged event by ID: {Id}", id);

        if (id is not Guid guidId)
        {
            _logger.LogWarning("Invalid ID type for FlaggedEvent: {IdType}", id?.GetType().Name ?? "null");
            return null;
        }

        var result = await DbSet
            .AsNoTracking()
            .Include(e => e.Guild)
            .FirstOrDefaultAsync(e => e.Id == guidId, cancellationToken);

        _logger.LogDebug("Flagged event {Id} found: {Found}", id, result != null);
        return result;
    }

    public async Task<(IEnumerable<FlaggedEvent> Items, int TotalCount)> GetPendingEventsAsync(
        ulong guildId,
        RuleType? ruleType = null,
        Severity? severity = null,
        ulong? userId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving pending flagged events for guild {GuildId}, ruleType {RuleType}, severity {Severity}, userId {UserId}, page {Page}, pageSize {PageSize}",
            guildId, ruleType, severity, userId, page, pageSize);

        var query = DbSet
            .AsNoTracking()
            .Include(e => e.Guild)
            .Where(e => e.GuildId == guildId && e.Status == FlaggedEventStatus.Pending);

        if (ruleType.HasValue)
        {
            query = query.Where(e => e.RuleType == ruleType.Value);
        }

        if (severity.HasValue)
        {
            query = query.Where(e => e.Severity == severity.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(e => e.UserId == userId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (page - 1) * pageSize;
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} pending flagged events for guild {GuildId} out of {TotalCount} total",
            items.Count, guildId, totalCount);

        return (items, totalCount);
    }

    public async Task<(IEnumerable<FlaggedEvent> Items, int TotalCount)> GetByUserAsync(
        ulong guildId,
        ulong userId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving flagged events for user {UserId} in guild {GuildId}, page {Page}, pageSize {PageSize}",
            userId, guildId, page, pageSize);

        var query = DbSet
            .AsNoTracking()
            .Include(e => e.Guild)
            .Where(e => e.GuildId == guildId && e.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (page - 1) * pageSize;
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} flagged events for user {UserId} out of {TotalCount} total",
            items.Count, userId, totalCount);

        return (items, totalCount);
    }

    public async Task<IDictionary<FlaggedEventStatus, int>> GetCountByStatusAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting flagged event count by status for guild {GuildId}", guildId);

        var result = await DbSet
            .AsNoTracking()
            .Where(e => e.GuildId == guildId)
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        _logger.LogDebug("Retrieved status counts for guild {GuildId}: {Counts}", guildId, result.Count);
        return result;
    }

    public async Task<(IEnumerable<FlaggedEvent> Items, int TotalCount)> GetFilteredByGuildAsync(
        ulong guildId,
        FlaggedEventStatus? status = null,
        RuleType? ruleType = null,
        Severity? severity = null,
        ulong? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving filtered flagged events for guild {GuildId}, status {Status}, ruleType {RuleType}, severity {Severity}, userId {UserId}, page {Page}",
            guildId, status, ruleType, severity, userId, page);

        var query = DbSet
            .AsNoTracking()
            .Include(e => e.Guild)
            .Where(e => e.GuildId == guildId);

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        if (ruleType.HasValue)
        {
            query = query.Where(e => e.RuleType == ruleType.Value);
        }

        if (severity.HasValue)
        {
            query = query.Where(e => e.Severity == severity.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(e => e.UserId == userId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(e => e.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(e => e.CreatedAt <= endDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (page - 1) * pageSize;
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} filtered flagged events for guild {GuildId} out of {TotalCount} total",
            items.Count, guildId, totalCount);

        return (items, totalCount);
    }
}
