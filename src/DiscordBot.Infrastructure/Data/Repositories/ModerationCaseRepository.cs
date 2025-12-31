using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for ModerationCase entities with moderation-specific operations.
/// </summary>
public class ModerationCaseRepository : Repository<ModerationCase>, IModerationCaseRepository
{
    private readonly ILogger<ModerationCaseRepository> _logger;

    public ModerationCaseRepository(
        BotDbContext context,
        ILogger<ModerationCaseRepository> logger,
        ILogger<Repository<ModerationCase>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Overrides base implementation to include Guild and RelatedFlaggedEvent navigation properties.
    /// </remarks>
    public override async Task<ModerationCase?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving moderation case by ID: {Id}", id);

        if (id is not Guid guidId)
        {
            _logger.LogWarning("Invalid ID type for ModerationCase: {IdType}", id?.GetType().Name ?? "null");
            return null;
        }

        var result = await DbSet
            .AsNoTracking()
            .Include(c => c.Guild)
            .Include(c => c.RelatedFlaggedEvent)
            .FirstOrDefaultAsync(c => c.Id == guidId, cancellationToken);

        _logger.LogDebug("Moderation case {Id} found: {Found}", id, result != null);
        return result;
    }

    public async Task<(IEnumerable<ModerationCase> Items, int TotalCount)> GetByGuildAsync(
        ulong guildId,
        CaseType? type = null,
        ulong? targetUserId = null,
        ulong? moderatorUserId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving moderation cases for guild {GuildId}, type {Type}, targetUserId {TargetUserId}, moderatorUserId {ModeratorUserId}, page {Page}",
            guildId, type, targetUserId, moderatorUserId, page);

        var query = DbSet
            .AsNoTracking()
            .Include(c => c.Guild)
            .Include(c => c.RelatedFlaggedEvent)
            .Where(c => c.GuildId == guildId);

        if (type.HasValue)
        {
            query = query.Where(c => c.Type == type.Value);
        }

        if (targetUserId.HasValue)
        {
            query = query.Where(c => c.TargetUserId == targetUserId.Value);
        }

        if (moderatorUserId.HasValue)
        {
            query = query.Where(c => c.ModeratorUserId == moderatorUserId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(c => c.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(c => c.CreatedAt <= endDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (page - 1) * pageSize;
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} moderation cases for guild {GuildId} out of {TotalCount} total",
            items.Count, guildId, totalCount);

        return (items, totalCount);
    }

    public async Task<(IEnumerable<ModerationCase> Items, int TotalCount)> GetByUserAsync(
        ulong guildId,
        ulong userId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving moderation cases for user {UserId} in guild {GuildId}, page {Page}, pageSize {PageSize}",
            userId, guildId, page, pageSize);

        var query = DbSet
            .AsNoTracking()
            .Include(c => c.Guild)
            .Include(c => c.RelatedFlaggedEvent)
            .Where(c => c.GuildId == guildId && c.TargetUserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (page - 1) * pageSize;
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} moderation cases for user {UserId} out of {TotalCount} total",
            items.Count, userId, totalCount);

        return (items, totalCount);
    }

    public async Task<long> GetNextCaseNumberAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting next case number for guild {GuildId}", guildId);

        var maxCaseNumber = await DbSet
            .AsNoTracking()
            .Where(c => c.GuildId == guildId)
            .MaxAsync(c => (long?)c.CaseNumber, cancellationToken);

        var nextCaseNumber = (maxCaseNumber ?? 0) + 1;

        _logger.LogDebug("Next case number for guild {GuildId} is {CaseNumber}", guildId, nextCaseNumber);
        return nextCaseNumber;
    }

    public async Task<ModerationCase?> GetByCaseNumberAsync(
        ulong guildId,
        long caseNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving moderation case {CaseNumber} for guild {GuildId}", caseNumber, guildId);

        var result = await DbSet
            .AsNoTracking()
            .Include(c => c.Guild)
            .Include(c => c.RelatedFlaggedEvent)
            .FirstOrDefaultAsync(c => c.GuildId == guildId && c.CaseNumber == caseNumber, cancellationToken);

        _logger.LogDebug("Moderation case {CaseNumber} for guild {GuildId} found: {Found}", caseNumber, guildId, result != null);
        return result;
    }

    public async Task<IEnumerable<ModerationCase>> GetExpiredCasesAsync(
        DateTime beforeTime,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving expired moderation cases before {BeforeTime}", beforeTime);

        var results = await DbSet
            .AsNoTracking()
            .Include(c => c.Guild)
            .Where(c => c.ExpiresAt != null && c.ExpiresAt <= beforeTime)
            .Where(c => c.Type == CaseType.Ban || c.Type == CaseType.Mute)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} expired moderation cases", results.Count);
        return results;
    }
}
