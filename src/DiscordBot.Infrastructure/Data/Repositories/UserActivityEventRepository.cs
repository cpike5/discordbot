using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Tracing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for UserActivityEvent entities.
/// Provides query operations for consent-free analytics.
/// </summary>
public class UserActivityEventRepository : Repository<UserActivityEvent>, IUserActivityEventRepository
{
    private readonly ILogger<UserActivityEventRepository> _logger;

    public UserActivityEventRepository(
        BotDbContext context,
        ILogger<UserActivityEventRepository> logger,
        ILogger<Repository<UserActivityEvent>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<UserActivityEvent>> GetByGuildAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            nameof(GetByGuildAsync),
            nameof(UserActivityEvent),
            "SELECT");

        _logger.LogDebug(
            "Retrieving activity events for guild {GuildId}, since: {Since}, until: {Until}",
            guildId, since, until);

        var events = await DbSet
            .AsNoTracking()
            .Where(e => e.GuildId == guildId && e.Timestamp >= since && e.Timestamp <= until)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} activity events for guild {GuildId}", events.Count, guildId);
        return events;
    }

    public async Task<IEnumerable<UserActivityEvent>> GetByUserAsync(
        ulong userId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            nameof(GetByUserAsync),
            nameof(UserActivityEvent),
            "SELECT");

        _logger.LogDebug(
            "Retrieving activity events for user {UserId}, since: {Since}, until: {Until}",
            userId, since, until);

        var events = await DbSet
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Timestamp >= since && e.Timestamp <= until)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} activity events for user {UserId}", events.Count, userId);
        return events;
    }

    public async Task<Dictionary<ActivityEventType, long>> GetEventCountsAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        ActivityEventType? eventType = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            nameof(GetEventCountsAsync),
            nameof(UserActivityEvent),
            "SELECT");

        _logger.LogDebug(
            "Retrieving event counts for guild {GuildId}, since: {Since}, until: {Until}, eventType: {EventType}",
            guildId, since, until, eventType);

        var query = DbSet
            .AsNoTracking()
            .Where(e => e.GuildId == guildId && e.Timestamp >= since && e.Timestamp <= until);

        if (eventType.HasValue)
        {
            query = query.Where(e => e.EventType == eventType.Value);
        }

        var results = await query
            .GroupBy(e => e.EventType)
            .Select(g => new { EventType = g.Key, Count = (long)g.Count() })
            .ToListAsync(cancellationToken);

        var counts = results.ToDictionary(r => r.EventType, r => r.Count);

        _logger.LogDebug("Retrieved event counts for {Count} event types", counts.Count);
        return counts;
    }

    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            nameof(DeleteOlderThanAsync),
            nameof(UserActivityEvent),
            "DELETE");

        _logger.LogDebug("Deleting batch of {BatchSize} activity events older than {Cutoff}", batchSize, cutoff);

        var deletedCount = await DbSet
            .Where(e => e.LoggedAt < cutoff)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogDebug("Deleted {Count} activity events in batch", deletedCount);
        return deletedCount;
    }

    public async Task<int> DeleteByUserIdAsync(
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            nameof(DeleteByUserIdAsync),
            nameof(UserActivityEvent),
            "DELETE");

        _logger.LogInformation("Deleting all activity events for user {UserId}", userId);

        var deletedCount = await DbSet
            .Where(e => e.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} activity events for user {UserId}", deletedCount, userId);
        return deletedCount;
    }
}
