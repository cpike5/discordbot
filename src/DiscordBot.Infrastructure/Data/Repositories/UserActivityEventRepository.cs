using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for UserActivityEvent entities with analytics-specific operations.
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

    /// <inheritdoc/>
    public async Task<int> AddBatchAsync(
        IEnumerable<UserActivityEvent> events,
        CancellationToken cancellationToken = default)
    {
        var eventList = events.ToList();

        if (eventList.Count == 0)
        {
            return 0;
        }

        _logger.LogDebug("Adding batch of {Count} activity events", eventList.Count);

        await DbSet.AddRangeAsync(eventList, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Successfully added {Count} activity events", eventList.Count);
        return eventList.Count;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<UserActivityEvent>> GetGuildEventsAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving events for guild {GuildId} between {Since} and {Until}",
            guildId, since, until);

        var events = await DbSet
            .AsNoTracking()
            .Where(e => e.GuildId == guildId && e.Timestamp >= since && e.Timestamp < until)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} events for guild {GuildId}", events.Count, guildId);
        return events;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<UserActivityEvent>> GetUserEventsAsync(
        ulong guildId,
        ulong userId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving events for user {UserId} in guild {GuildId} between {Since} and {Until}",
            userId, guildId, since, until);

        var events = await DbSet
            .AsNoTracking()
            .Where(e => e.GuildId == guildId && e.UserId == userId && e.Timestamp >= since && e.Timestamp < until)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} events for user {UserId}", events.Count, userId);
        return events;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<UserActivityEvent>> GetChannelEventsAsync(
        ulong guildId,
        ulong channelId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving events for channel {ChannelId} in guild {GuildId} between {Since} and {Until}",
            channelId, guildId, since, until);

        var events = await DbSet
            .AsNoTracking()
            .Where(e => e.GuildId == guildId && e.ChannelId == channelId && e.Timestamp >= since && e.Timestamp < until)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} events for channel {ChannelId}", events.Count, channelId);
        return events;
    }

    /// <inheritdoc/>
    public async Task<Dictionary<ActivityEventType, int>> GetEventCountsByTypeAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving event counts by type for guild {GuildId} between {Since} and {Until}",
            guildId, since, until);

        var counts = await DbSet
            .AsNoTracking()
            .Where(e => e.GuildId == guildId && e.Timestamp >= since && e.Timestamp < until)
            .GroupBy(e => e.EventType)
            .Select(g => new { EventType = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var result = counts.ToDictionary(c => c.EventType, c => c.Count);

        _logger.LogDebug("Retrieved counts for {Count} event types", result.Count);
        return result;
    }

    /// <inheritdoc/>
    public async Task<HashSet<ulong>> GetActiveUserIdsAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving active user IDs for guild {GuildId} between {Since} and {Until}",
            guildId, since, until);

        var userIds = await DbSet
            .AsNoTracking()
            .Where(e => e.GuildId == guildId && e.Timestamp >= since && e.Timestamp < until)
            .Select(e => e.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var result = userIds.ToHashSet();

        _logger.LogDebug("Retrieved {Count} active users for guild {GuildId}", result.Count, guildId);
        return result;
    }

    /// <inheritdoc/>
    public async Task<HashSet<ulong>> GetActiveChannelIdsAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving active channel IDs for guild {GuildId} between {Since} and {Until}",
            guildId, since, until);

        var channelIds = await DbSet
            .AsNoTracking()
            .Where(e => e.GuildId == guildId && e.Timestamp >= since && e.Timestamp < until)
            .Select(e => e.ChannelId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var result = channelIds.ToHashSet();

        _logger.LogDebug("Retrieved {Count} active channels for guild {GuildId}", result.Count, guildId);
        return result;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteEventsOlderThanAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting activity events older than {Cutoff}", cutoff);

        var deletedCount = await DbSet
            .Where(e => e.Timestamp < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} activity events older than {Cutoff}", deletedCount, cutoff);
        return deletedCount;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteBatchOlderThanAsync(
        DateTime cutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting batch of {BatchSize} activity events older than {Cutoff}", batchSize, cutoff);

        var deletedCount = await DbSet
            .Where(e => e.Timestamp < cutoff)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogDebug("Deleted {Count} activity events in batch", deletedCount);
        return deletedCount;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<(ulong UserId, int MessageCount, int ReactionCount, int UniqueChannels)>> GetUserActivitySummaryAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving user activity summary for guild {GuildId} between {Since} and {Until}",
            guildId, since, until);

        var summary = await DbSet
            .AsNoTracking()
            .Where(e => e.GuildId == guildId && e.Timestamp >= since && e.Timestamp < until)
            .GroupBy(e => e.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                MessageCount = g.Count(e => e.EventType == ActivityEventType.Message || e.EventType == ActivityEventType.Reply),
                ReactionCount = g.Count(e => e.EventType == ActivityEventType.Reaction),
                UniqueChannels = g.Select(e => e.ChannelId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var result = summary.Select(s => (s.UserId, s.MessageCount, s.ReactionCount, s.UniqueChannels)).ToList();

        _logger.LogDebug("Retrieved activity summary for {Count} users", result.Count);
        return result;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<(ulong ChannelId, int MessageCount, int ReactionCount, int UniqueUsers)>> GetChannelActivitySummaryAsync(
        ulong guildId,
        DateTime since,
        DateTime until,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving channel activity summary for guild {GuildId} between {Since} and {Until}",
            guildId, since, until);

        var summary = await DbSet
            .AsNoTracking()
            .Where(e => e.GuildId == guildId && e.Timestamp >= since && e.Timestamp < until)
            .GroupBy(e => e.ChannelId)
            .Select(g => new
            {
                ChannelId = g.Key,
                MessageCount = g.Count(e => e.EventType == ActivityEventType.Message || e.EventType == ActivityEventType.Reply),
                ReactionCount = g.Count(e => e.EventType == ActivityEventType.Reaction),
                UniqueUsers = g.Select(e => e.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var result = summary.Select(s => (s.ChannelId, s.MessageCount, s.ReactionCount, s.UniqueUsers)).ToList();

        _logger.LogDebug("Retrieved activity summary for {Count} channels", result.Count);
        return result;
    }

    /// <inheritdoc/>
    public async Task<DateTime?> GetOldestEventDateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving oldest activity event date");

        var oldestDate = await DbSet
            .AsNoTracking()
            .OrderBy(e => e.Timestamp)
            .Select(e => e.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (oldestDate == default)
        {
            _logger.LogDebug("No activity events found in database");
            return null;
        }

        _logger.LogDebug("Oldest activity event date: {Date}", oldestDate);
        return oldestDate;
    }
}
