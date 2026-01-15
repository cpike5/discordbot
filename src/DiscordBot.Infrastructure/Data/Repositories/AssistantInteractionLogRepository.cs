using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for AssistantInteractionLog entities.
/// Provides data access operations for assistant conversation history and audit trails.
/// </summary>
public class AssistantInteractionLogRepository : Repository<AssistantInteractionLog>, IAssistantInteractionLogRepository
{
    private readonly ILogger<AssistantInteractionLogRepository> _logger;

    public AssistantInteractionLogRepository(
        BotDbContext context,
        ILogger<AssistantInteractionLogRepository> logger,
        ILogger<Repository<AssistantInteractionLog>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AssistantInteractionLog>> GetRecentByGuildAsync(
        ulong guildId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving {Limit} recent interaction logs for guild {GuildId}",
            limit, guildId);

        var logs = await DbSet
            .AsNoTracking()
            .Include(l => l.User)
            .Where(l => l.GuildId == guildId)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} interaction logs for guild {GuildId}",
            logs.Count, guildId);

        return logs;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AssistantInteractionLog>> GetRecentByUserAsync(
        ulong userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving {Limit} recent interaction logs for user {UserId}",
            limit, userId);

        var logs = await DbSet
            .AsNoTracking()
            .Include(l => l.Guild)
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} interaction logs for user {UserId}",
            logs.Count, userId);

        return logs;
    }

    /// <inheritdoc />
    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Deleting assistant interaction logs older than {CutoffDate}",
            cutoffDate);

        var deletedCount = await DbSet
            .Where(l => l.Timestamp < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted {Count} assistant interaction logs older than {CutoffDate}",
            deletedCount, cutoffDate);

        return deletedCount;
    }

    /// <summary>
    /// Logs a new interaction.
    /// </summary>
    /// <param name="log">The interaction log entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created log entry with ID.</returns>
    public async Task<AssistantInteractionLog> LogInteractionAsync(
        AssistantInteractionLog log,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Logging assistant interaction for user {UserId} in guild {GuildId}",
            log.UserId, log.GuildId);

        log.Timestamp = DateTime.UtcNow;
        await DbSet.AddAsync(log, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Logged assistant interaction {Id} for user {UserId} in guild {GuildId}. Success: {Success}",
            log.Id, log.UserId, log.GuildId, log.Success);

        return log;
    }

    /// <summary>
    /// Gets interaction statistics for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="startDate">Start date for statistics (UTC).</param>
    /// <param name="endDate">End date for statistics (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Interaction statistics.</returns>
    public async Task<InteractionStatistics> GetStatisticsAsync(
        ulong guildId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting interaction statistics for guild {GuildId} from {StartDate} to {EndDate}",
            guildId, startDate, endDate);

        var stats = await DbSet
            .AsNoTracking()
            .Where(l => l.GuildId == guildId &&
                       l.Timestamp >= startDate &&
                       l.Timestamp <= endDate)
            .GroupBy(l => 1)
            .Select(g => new InteractionStatistics
            {
                TotalInteractions = g.Count(),
                SuccessfulInteractions = g.Count(l => l.Success),
                FailedInteractions = g.Count(l => !l.Success),
                TotalInputTokens = g.Sum(l => l.InputTokens),
                TotalOutputTokens = g.Sum(l => l.OutputTokens),
                TotalCachedTokens = g.Sum(l => l.CachedTokens),
                TotalToolCalls = g.Sum(l => l.ToolCalls),
                TotalCost = g.Sum(l => l.EstimatedCostUsd),
                AverageLatencyMs = (int)g.Average(l => l.LatencyMs),
                UniqueUsers = g.Select(l => l.UserId).Distinct().Count(),
                CacheHitCount = g.Count(l => l.CacheHit)
            })
            .FirstOrDefaultAsync(cancellationToken);

        return stats ?? new InteractionStatistics();
    }

    /// <summary>
    /// Gets the most recent interactions for a user in a guild (for context).
    /// </summary>
    /// <param name="userId">Discord user ID.</param>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="limit">Maximum number of interactions to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recent interactions ordered by timestamp ascending (oldest first).</returns>
    public async Task<IEnumerable<AssistantInteractionLog>> GetRecentContextAsync(
        ulong userId,
        ulong guildId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Getting {Limit} recent interactions for context: user {UserId} in guild {GuildId}",
            limit, userId, guildId);

        var logs = await DbSet
            .AsNoTracking()
            .Where(l => l.UserId == userId && l.GuildId == guildId && l.Success)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);

        // Return in chronological order (oldest first) for conversation context
        return logs.OrderBy(l => l.Timestamp);
    }
}

/// <summary>
/// Statistics for assistant interactions.
/// </summary>
public class InteractionStatistics
{
    public int TotalInteractions { get; set; }
    public int SuccessfulInteractions { get; set; }
    public int FailedInteractions { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public int TotalCachedTokens { get; set; }
    public int TotalToolCalls { get; set; }
    public decimal TotalCost { get; set; }
    public int AverageLatencyMs { get; set; }
    public int UniqueUsers { get; set; }
    public int CacheHitCount { get; set; }

    public double SuccessRate => TotalInteractions > 0
        ? (double)SuccessfulInteractions / TotalInteractions
        : 0;

    public double CacheHitRate => TotalInteractions > 0
        ? (double)CacheHitCount / TotalInteractions
        : 0;
}
