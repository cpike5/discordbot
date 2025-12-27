using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for AuditLog entities with specialized querying and bulk operations.
/// </summary>
public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    private readonly ILogger<AuditLogRepository> _logger;

    public AuditLogRepository(
        BotDbContext context,
        ILogger<AuditLogRepository> logger,
        ILogger<Repository<AuditLog>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> GetLogsAsync(
        AuditLogQueryDto query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving audit logs with filters - Category: {Category}, Action: {Action}, ActorId: {ActorId}, GuildId: {GuildId}, Page: {Page}, PageSize: {PageSize}",
            query.Category, query.Action, query.ActorId, query.GuildId, query.Page, query.PageSize);

        var queryable = DbSet.AsNoTracking();

        // Apply filters
        if (query.Category.HasValue)
        {
            queryable = queryable.Where(l => l.Category == query.Category.Value);
        }

        if (query.Action.HasValue)
        {
            queryable = queryable.Where(l => l.Action == query.Action.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.ActorId))
        {
            queryable = queryable.Where(l => l.ActorId == query.ActorId);
        }

        if (query.ActorType.HasValue)
        {
            queryable = queryable.Where(l => l.ActorType == query.ActorType.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetType))
        {
            queryable = queryable.Where(l => l.TargetType == query.TargetType);
        }

        if (!string.IsNullOrWhiteSpace(query.TargetId))
        {
            queryable = queryable.Where(l => l.TargetId == query.TargetId);
        }

        if (query.GuildId.HasValue)
        {
            queryable = queryable.Where(l => l.GuildId == query.GuildId.Value);
        }

        if (query.StartDate.HasValue)
        {
            queryable = queryable.Where(l => l.Timestamp >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            queryable = queryable.Where(l => l.Timestamp <= query.EndDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            queryable = queryable.Where(l => l.CorrelationId == query.CorrelationId);
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            queryable = queryable.Where(l => l.Details != null && l.Details.Contains(query.SearchTerm));
        }

        // Get total count before pagination
        var totalCount = await queryable.CountAsync(cancellationToken);

        // Apply sorting
        queryable = query.SortBy.ToLowerInvariant() switch
        {
            "category" => query.SortDescending
                ? queryable.OrderByDescending(l => l.Category)
                : queryable.OrderBy(l => l.Category),
            "action" => query.SortDescending
                ? queryable.OrderByDescending(l => l.Action)
                : queryable.OrderBy(l => l.Action),
            "actorid" => query.SortDescending
                ? queryable.OrderByDescending(l => l.ActorId)
                : queryable.OrderBy(l => l.ActorId),
            "guildid" => query.SortDescending
                ? queryable.OrderByDescending(l => l.GuildId)
                : queryable.OrderBy(l => l.GuildId),
            _ => query.SortDescending
                ? queryable.OrderByDescending(l => l.Timestamp)
                : queryable.OrderBy(l => l.Timestamp)
        };

        // Apply pagination
        var skip = (query.Page - 1) * query.PageSize;
        var items = await queryable
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} audit logs out of {TotalCount} total matching filters",
            items.Count, totalCount);

        return (items, totalCount);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditLog>> GetByCorrelationIdAsync(
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving audit logs by correlation ID: {CorrelationId}", correlationId);

        var logs = await DbSet
            .AsNoTracking()
            .Where(l => l.CorrelationId == correlationId)
            .OrderBy(l => l.Timestamp)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Found {Count} audit logs with correlation ID {CorrelationId}",
            logs.Count, correlationId);

        return logs;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AuditLog>> GetRecentByActorAsync(
        string actorId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving recent audit logs for actor {ActorId}, limit: {Limit}",
            actorId, limit);

        var logs = await DbSet
            .AsNoTracking()
            .Where(l => l.ActorId == actorId)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} recent audit logs for actor {ActorId}",
            logs.Count, actorId);

        return logs;
    }

    /// <inheritdoc/>
    public async Task BulkInsertAsync(
        IEnumerable<AuditLog> logs,
        CancellationToken cancellationToken = default)
    {
        var logList = logs.ToList();
        _logger.LogDebug("Performing bulk insert of {Count} audit logs", logList.Count);

        if (!logList.Any())
        {
            _logger.LogDebug("No audit logs to insert, skipping bulk insert");
            return;
        }

        await DbSet.AddRangeAsync(logList, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Successfully bulk inserted {Count} audit logs", logList.Count);
    }

    /// <inheritdoc/>
    public async Task<AuditLogStatsDto> GetStatsAsync(
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving audit log statistics for guild: {GuildId}", guildId?.ToString() ?? "All");

        var query = DbSet.AsNoTracking();

        if (guildId.HasValue)
        {
            query = query.Where(l => l.GuildId == guildId.Value);
        }

        var now = DateTime.UtcNow;
        var last24Hours = now.AddHours(-24);
        var last7Days = now.AddDays(-7);
        var last30Days = now.AddDays(-30);

        var stats = new AuditLogStatsDto
        {
            TotalEntries = await query.LongCountAsync(cancellationToken),
            Last24Hours = await query.CountAsync(l => l.Timestamp >= last24Hours, cancellationToken),
            Last7Days = await query.CountAsync(l => l.Timestamp >= last7Days, cancellationToken),
            Last30Days = await query.CountAsync(l => l.Timestamp >= last30Days, cancellationToken),
            OldestEntry = await query.OrderBy(l => l.Timestamp).Select(l => l.Timestamp).FirstOrDefaultAsync(cancellationToken),
            NewestEntry = await query.OrderByDescending(l => l.Timestamp).Select(l => l.Timestamp).FirstOrDefaultAsync(cancellationToken)
        };

        // Get breakdown by category
        var byCategory = await query
            .GroupBy(l => l.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var item in byCategory)
        {
            stats.ByCategory[item.Category] = item.Count;
        }

        // Get breakdown by action
        var byAction = await query
            .GroupBy(l => l.Action)
            .Select(g => new { Action = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var item in byAction)
        {
            stats.ByAction[item.Action] = item.Count;
        }

        // Get breakdown by actor type
        var byActorType = await query
            .GroupBy(l => l.ActorType)
            .Select(g => new { ActorType = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        foreach (var item in byActorType)
        {
            stats.ByActorType[item.ActorType] = item.Count;
        }

        // Get top 10 actors
        var topActors = await query
            .Where(l => l.ActorId != null)
            .GroupBy(l => l.ActorId!)
            .Select(g => new { ActorId = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var item in topActors)
        {
            stats.TopActors[item.ActorId] = item.Count;
        }

        _logger.LogDebug(
            "Retrieved audit log statistics - Total: {Total}, Last24h: {Last24h}, Last7d: {Last7d}",
            stats.TotalEntries, stats.Last24Hours, stats.Last7Days);

        return stats;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteOlderThanAsync(
        DateTime olderThan,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting audit logs older than {CutoffDate}", olderThan);

        var logsToDelete = await DbSet
            .Where(l => l.Timestamp < olderThan)
            .ToListAsync(cancellationToken);

        var count = logsToDelete.Count;

        if (count > 0)
        {
            DbSet.RemoveRange(logsToDelete);
            await Context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted {Count} audit logs older than {CutoffDate}", count, olderThan);
        }
        else
        {
            _logger.LogDebug("No audit logs found older than {CutoffDate}", olderThan);
        }

        return count;
    }
}
