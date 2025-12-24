using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
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

    /// <inheritdoc/>
    /// <remarks>
    /// Overrides base implementation to include User and Guild navigation properties.
    /// </remarks>
    public override async Task<MessageLog?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving message log by ID: {Id}", id);

        if (id is not long longId)
        {
            _logger.LogWarning("Invalid ID type for MessageLog: {IdType}", id?.GetType().Name ?? "null");
            return null;
        }

        var result = await DbSet
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.Guild)
            .FirstOrDefaultAsync(m => m.Id == longId, cancellationToken);

        _logger.LogDebug("Message log {Id} found: {Found}", id, result != null);
        return result;
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

    public async Task<(IEnumerable<MessageLog> Items, int TotalCount)> GetPaginatedAsync(
        MessageLogQueryDto query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving paginated messages: Page {Page}, PageSize {PageSize}, AuthorId: {AuthorId}, GuildId: {GuildId}, ChannelId: {ChannelId}, Source: {Source}",
            query.Page, query.PageSize, query.AuthorId, query.GuildId, query.ChannelId, query.Source);

        IQueryable<MessageLog> dbQuery = DbSet
            .AsNoTracking()
            .Include(m => m.User)
            .Include(m => m.Guild);

        // Apply filters
        if (query.AuthorId.HasValue)
        {
            dbQuery = dbQuery.Where(m => m.AuthorId == query.AuthorId.Value);
        }

        if (query.GuildId.HasValue)
        {
            dbQuery = dbQuery.Where(m => m.GuildId == query.GuildId.Value);
        }

        if (query.ChannelId.HasValue)
        {
            dbQuery = dbQuery.Where(m => m.ChannelId == query.ChannelId.Value);
        }

        if (query.Source.HasValue)
        {
            dbQuery = dbQuery.Where(m => m.Source == query.Source.Value);
        }

        // Apply date range filters
        if (query.StartDate.HasValue)
        {
            dbQuery = dbQuery.Where(m => m.Timestamp >= query.StartDate.Value);
        }

        if (query.EndDate.HasValue)
        {
            dbQuery = dbQuery.Where(m => m.Timestamp <= query.EndDate.Value);
        }

        // Apply search term filter
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            dbQuery = dbQuery.Where(m => m.Content.ToLower().Contains(query.SearchTerm.ToLower()));
        }

        // Get total count before pagination
        var totalCount = await dbQuery.CountAsync(cancellationToken);

        // Apply pagination
        var skip = (query.Page - 1) * query.PageSize;
        var items = await dbQuery
            .OrderByDescending(m => m.Timestamp)
            .Skip(skip)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} messages out of {TotalCount} total", items.Count, totalCount);
        return (items, totalCount);
    }

    public async Task<int> DeleteByUserIdAsync(ulong userId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting all messages for user {UserId}", userId);

        var deletedCount = await DbSet
            .Where(m => m.AuthorId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation("Deleted {Count} messages for user {UserId}", deletedCount, userId);
        return deletedCount;
    }

    public async Task<(long Total, long DmCount, long ServerCount, long UniqueAuthors)> GetBasicStatsAsync(
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving basic stats for guildId: {GuildId}", guildId);

        var query = DbSet.AsNoTracking();

        // Apply guild filter if specified
        if (guildId.HasValue)
        {
            query = query.Where(m => m.GuildId == guildId.Value);
        }

        // Get total count
        var total = await query.LongCountAsync(cancellationToken);

        // Get DM count
        var dmCount = await query
            .Where(m => m.Source == MessageSource.DirectMessage)
            .LongCountAsync(cancellationToken);

        // Get server channel count
        var serverCount = await query
            .Where(m => m.Source == MessageSource.ServerChannel)
            .LongCountAsync(cancellationToken);

        // Get unique authors count
        var uniqueAuthors = await query
            .Select(m => m.AuthorId)
            .Distinct()
            .LongCountAsync(cancellationToken);

        _logger.LogDebug(
            "Stats - Total: {Total}, DM: {DmCount}, Server: {ServerCount}, UniqueAuthors: {UniqueAuthors}",
            total, dmCount, serverCount, uniqueAuthors);

        return (total, dmCount, serverCount, uniqueAuthors);
    }

    public async Task<IEnumerable<(DateOnly Date, long Count)>> GetMessagesByDayAsync(
        int days = 7,
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving messages by day for last {Days} days, guildId: {GuildId}", days, guildId);

        var cutoffDate = DateTime.UtcNow.AddDays(-days);
        var query = DbSet.AsNoTracking()
            .Where(m => m.Timestamp >= cutoffDate);

        // Apply guild filter if specified
        if (guildId.HasValue)
        {
            query = query.Where(m => m.GuildId == guildId.Value);
        }

        var results = await query
            .GroupBy(m => DateOnly.FromDateTime(m.Timestamp))
            .Select(g => new { Date = g.Key, Count = (long)g.Count() })
            .OrderByDescending(x => x.Date)
            .ToListAsync(cancellationToken);

        var tupleResults = results.Select(r => (r.Date, r.Count)).ToList();

        _logger.LogDebug("Retrieved message counts for {Count} days", tupleResults.Count);
        return tupleResults;
    }

    public async Task<int> DeleteBatchOlderThanAsync(
        DateTime cutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting batch of {BatchSize} messages older than {Cutoff}", batchSize, cutoff);

        var deletedCount = await DbSet
            .Where(m => m.Timestamp < cutoff)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogDebug("Deleted {Count} messages in batch", deletedCount);
        return deletedCount;
    }

    public async Task<DateTime?> GetOldestMessageDateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving oldest message date");

        var oldestDate = await DbSet
            .AsNoTracking()
            .OrderBy(m => m.Timestamp)
            .Select(m => m.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (oldestDate == default)
        {
            _logger.LogDebug("No messages found in database");
            return null;
        }

        _logger.LogDebug("Oldest message date: {Date}", oldestDate);
        return oldestDate;
    }

    public async Task<DateTime?> GetNewestMessageDateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving newest message date");

        var newestDate = await DbSet
            .AsNoTracking()
            .OrderByDescending(m => m.Timestamp)
            .Select(m => m.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);

        if (newestDate == default)
        {
            _logger.LogDebug("No messages found in database");
            return null;
        }

        _logger.LogDebug("Newest message date: {Date}", newestDate);
        return newestDate;
    }
}
