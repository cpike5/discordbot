using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for ConnectionEvent entities.
/// Provides persistence for Discord gateway connection state changes.
/// </summary>
public class ConnectionEventRepository : Repository<ConnectionEvent>, IConnectionEventRepository
{
    private readonly ILogger<ConnectionEventRepository> _logger;

    public ConnectionEventRepository(
        BotDbContext context,
        ILogger<ConnectionEventRepository> logger,
        ILogger<Repository<ConnectionEvent>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ConnectionEvent>> GetEventsSinceAsync(
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving connection events since {Since}", since);

        var events = await DbSet
            .AsNoTracking()
            .Where(e => e.Timestamp >= since)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} connection events since {Since}", events.Count, since);

        return events;
    }

    /// <inheritdoc/>
    public async Task<ConnectionEvent?> GetLastEventAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving last connection event");

        var lastEvent = await DbSet
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastEvent != null)
        {
            _logger.LogDebug(
                "Last connection event: {EventType} at {Timestamp}",
                lastEvent.EventType, lastEvent.Timestamp);
        }
        else
        {
            _logger.LogDebug("No connection events found");
        }

        return lastEvent;
    }

    /// <inheritdoc/>
    public async Task<ConnectionEvent> AddEventAsync(
        string eventType,
        DateTime timestamp,
        string? reason = null,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Adding connection event: {EventType} at {Timestamp}",
            eventType, timestamp);

        var entity = new ConnectionEvent
        {
            EventType = eventType,
            Timestamp = timestamp,
            Reason = reason,
            Details = details
        };

        await DbSet.AddAsync(entity, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Connection event recorded: {EventType} at {Timestamp}, Id={Id}",
            eventType, timestamp, entity.Id);

        return entity;
    }

    /// <inheritdoc/>
    public async Task<int> CleanupOldEventsAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        _logger.LogInformation("Cleaning up connection events older than {Cutoff} ({RetentionDays} days)", cutoff, retentionDays);

        var eventsToDelete = await DbSet
            .Where(e => e.Timestamp < cutoff)
            .ToListAsync(cancellationToken);

        var count = eventsToDelete.Count;

        if (count > 0)
        {
            DbSet.RemoveRange(eventsToDelete);
            await Context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted {Count} connection events older than {Cutoff}", count, cutoff);
        }
        else
        {
            _logger.LogDebug("No connection events found older than {Cutoff}", cutoff);
        }

        return count;
    }
}
