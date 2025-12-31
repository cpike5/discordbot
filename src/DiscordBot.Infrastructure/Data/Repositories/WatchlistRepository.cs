using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for Watchlist entities with watchlist-specific operations.
/// </summary>
public class WatchlistRepository : Repository<Watchlist>, IWatchlistRepository
{
    private readonly ILogger<WatchlistRepository> _logger;

    public WatchlistRepository(
        BotDbContext context,
        ILogger<WatchlistRepository> logger,
        ILogger<Repository<Watchlist>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Overrides base implementation to include Guild navigation property.
    /// </remarks>
    public override async Task<Watchlist?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving watchlist entry by ID: {Id}", id);

        if (id is not Guid guidId)
        {
            _logger.LogWarning("Invalid ID type for Watchlist: {IdType}", id?.GetType().Name ?? "null");
            return null;
        }

        var result = await DbSet
            .AsNoTracking()
            .Include(w => w.Guild)
            .FirstOrDefaultAsync(w => w.Id == guidId, cancellationToken);

        _logger.LogDebug("Watchlist entry {Id} found: {Found}", id, result != null);
        return result;
    }

    public async Task<(IEnumerable<Watchlist> Items, int TotalCount)> GetByGuildAsync(
        ulong guildId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving watchlist entries for guild {GuildId}, page {Page}, pageSize {PageSize}",
            guildId, page, pageSize);

        var query = DbSet
            .AsNoTracking()
            .Include(w => w.Guild)
            .Where(w => w.GuildId == guildId);

        var totalCount = await query.CountAsync(cancellationToken);

        var skip = (page - 1) * pageSize;
        var items = await query
            .OrderByDescending(w => w.AddedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieved {Count} watchlist entries for guild {GuildId} out of {TotalCount} total",
            items.Count, guildId, totalCount);

        return (items, totalCount);
    }

    public async Task<bool> IsOnWatchlistAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking if user {UserId} is on watchlist in guild {GuildId}", userId, guildId);

        var exists = await DbSet
            .AsNoTracking()
            .AnyAsync(w => w.GuildId == guildId && w.UserId == userId, cancellationToken);

        _logger.LogDebug("User {UserId} on watchlist in guild {GuildId}: {Exists}", userId, guildId, exists);
        return exists;
    }

    public async Task<Watchlist?> GetByUserAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving watchlist entry for user {UserId} in guild {GuildId}", userId, guildId);

        var result = await DbSet
            .AsNoTracking()
            .Include(w => w.Guild)
            .FirstOrDefaultAsync(w => w.GuildId == guildId && w.UserId == userId, cancellationToken);

        _logger.LogDebug("Watchlist entry for user {UserId} in guild {GuildId} found: {Found}", userId, guildId, result != null);
        return result;
    }
}
