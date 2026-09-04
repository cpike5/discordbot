using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for ScheduledMessage entities with scheduled-message-specific operations.
/// </summary>
public class ScheduledMessageRepository : Repository<ScheduledMessage>, IScheduledMessageRepository
{
    private readonly ILogger<ScheduledMessageRepository> _logger;

    public ScheduledMessageRepository(
        BotDbContext context,
        ILogger<ScheduledMessageRepository> logger,
        ILogger<Repository<ScheduledMessage>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Overrides base implementation to include Guild navigation property.
    /// </remarks>
    public override async Task<ScheduledMessage?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving scheduled message by ID: {Id}", id);

        if (id is not Guid guidId)
        {
            _logger.LogWarning("Invalid ID type for ScheduledMessage: {IdType}", id?.GetType().Name ?? "null");
            return null;
        }

        var result = await DbSet
            .AsNoTracking()
            .Include(s => s.Guild)
            .FirstOrDefaultAsync(s => s.Id == guidId, cancellationToken);

        _logger.LogDebug("Scheduled message {Id} found: {Found}", id, result != null);
        return result;
    }

    public async Task<(IEnumerable<ScheduledMessage> Items, int TotalCount)> GetByGuildIdAsync(
        ulong guildId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving scheduled messages for guild {GuildId}, page {Page}, pageSize {PageSize}",
            guildId, page, pageSize);

        var query = DbSet
            .AsNoTracking()
            .Include(s => s.Guild)
            .Where(s => s.GuildId == guildId);

        var orderedQuery = query.OrderByDescending(s => s.CreatedAt);
        var (items, totalCount) = await GetPagedAsync(orderedQuery, page, pageSize, cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} scheduled messages for guild {GuildId} out of {TotalCount} total",
            items.Count, guildId, totalCount);

        return (items, totalCount);
    }

    public async Task<IEnumerable<ScheduledMessage>> GetDueMessagesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        _logger.LogDebug("Retrieving scheduled messages due for execution at {CurrentTime}", now);

        var dueMessages = await DbSet
            .AsNoTracking()
            .Include(s => s.Guild)
            .Where(s => s.IsEnabled && s.NextExecutionAt.HasValue && s.NextExecutionAt.Value <= now)
            .OrderBy(s => s.NextExecutionAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} scheduled messages due for execution", dueMessages.Count);
        return dueMessages;
    }

    public async Task<ScheduledMessage?> GetByIdWithGuildAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving scheduled message {Id} with guild navigation property", id);

        var result = await DbSet
            .AsNoTracking()
            .Include(s => s.Guild)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        _logger.LogDebug("Scheduled message {Id} found: {Found}", id, result != null);
        return result;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<ScheduledMessage>> SearchAsync(string searchTerm, int maxResults, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching scheduled messages for term: {SearchTerm}, maxResults: {MaxResults}", searchTerm, maxResults);

        var searchLower = searchTerm.ToLowerInvariant();

        var results = await DbSet
            .AsNoTracking()
            .Where(m => m.Content.ToLower().Contains(searchLower) ||
                       m.Title.ToLower().Contains(searchLower) ||
                       m.ChannelId.ToString().Contains(searchLower))
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} scheduled messages matching search term", results.Count);
        return results;
    }
}
