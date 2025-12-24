using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for MessageLog entities with message-specific operations.
/// </summary>
public class MessageLogRepository : Repository<MessageLog>, IMessageLogRepository
{
    private readonly ILogger<MessageLogRepository> _logger;

    public MessageLogRepository(
        BotDbContext context,
        ILogger<MessageLogRepository> logger,
        ILogger<Repository<MessageLog>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<MessageLog>> GetUserMessagesAsync(
        ulong authorId,
        DateTime? since = null,
        DateTime? until = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving {Limit} messages for user {AuthorId}, since: {Since}, until: {Until}",
            limit, authorId, since, until);

        var query = DbSet
            .AsNoTracking()
            .Where(m => m.AuthorId == authorId);

        if (since.HasValue)
        {
            query = query.Where(m => m.Timestamp >= since.Value);
        }

        if (until.HasValue)
        {
            query = query.Where(m => m.Timestamp <= until.Value);
        }

        var messages = await query
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} messages for user {AuthorId}", messages.Count, authorId);
        return messages;
    }

    public async Task<IEnumerable<MessageLog>> GetChannelMessagesAsync(
        ulong channelId,
        DateTime? since = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving {Limit} messages for channel {ChannelId}, since: {Since}",
            limit, channelId, since);

        var query = DbSet
            .AsNoTracking()
            .Where(m => m.ChannelId == channelId);

        if (since.HasValue)
        {
            query = query.Where(m => m.Timestamp >= since.Value);
        }

        var messages = await query
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} messages for channel {ChannelId}", messages.Count, channelId);
        return messages;
    }

    public async Task<IEnumerable<MessageLog>> GetGuildMessagesAsync(
        ulong guildId,
        DateTime? since = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving {Limit} messages for guild {GuildId}, since: {Since}",
            limit, guildId, since);

        var query = DbSet
            .AsNoTracking()
            .Where(m => m.GuildId == guildId);

        if (since.HasValue)
        {
            query = query.Where(m => m.Timestamp >= since.Value);
        }

        var messages = await query
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} messages for guild {GuildId}", messages.Count, guildId);
        return messages;
    }

    public async Task<int> DeleteMessagesOlderThanAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting messages logged before {Cutoff}", cutoff);

        var deletedCount = await DbSet
            .Where(m => m.LoggedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} messages older than {Cutoff}", deletedCount, cutoff);
        return deletedCount;
    }

    public async Task<long> GetMessageCountAsync(
        ulong? authorId = null,
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving message count, authorId: {AuthorId}, guildId: {GuildId}", authorId, guildId);

        var query = DbSet.AsNoTracking();

        if (authorId.HasValue)
        {
            query = query.Where(m => m.AuthorId == authorId.Value);
        }

        if (guildId.HasValue)
        {
            query = query.Where(m => m.GuildId == guildId.Value);
        }

        var count = await query.LongCountAsync(cancellationToken);

        _logger.LogDebug("Message count: {Count}", count);
        return count;
    }
}
