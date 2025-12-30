using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for RatRecord entities with record-specific operations.
/// </summary>
public class RatRecordRepository : Repository<RatRecord>, IRatRecordRepository
{
    private readonly ILogger<RatRecordRepository> _logger;

    public RatRecordRepository(
        BotDbContext context,
        ILogger<RatRecordRepository> logger,
        ILogger<Repository<RatRecord>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<int> GetGuiltyCountAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Counting guilty records for user {UserId} in guild {GuildId}", userId, guildId);

        var count = await DbSet
            .AsNoTracking()
            .Where(r => r.GuildId == guildId && r.UserId == userId)
            .CountAsync(cancellationToken);

        _logger.LogDebug("User {UserId} has {Count} guilty records", userId, count);
        return count;
    }

    public async Task<IEnumerable<RatRecord>> GetRecentRecordsAsync(
        ulong guildId,
        ulong userId,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving {Limit} recent guilty records for user {UserId} in guild {GuildId}",
            limit, userId, guildId);

        var records = await DbSet
            .AsNoTracking()
            .Where(r => r.GuildId == guildId && r.UserId == userId)
            .OrderByDescending(r => r.RecordedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} recent records for user {UserId}", records.Count, userId);
        return records;
    }

    public async Task<IEnumerable<(ulong UserId, int GuiltyCount)>> GetLeaderboardAsync(
        ulong guildId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving top {Limit} leaderboard for guild {GuildId}", limit, guildId);

        var leaderboard = await DbSet
            .AsNoTracking()
            .Where(r => r.GuildId == guildId)
            .GroupBy(r => r.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                GuiltyCount = g.Count()
            })
            .OrderByDescending(x => x.GuiltyCount)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var result = leaderboard.Select(x => (x.UserId, x.GuiltyCount));

        _logger.LogDebug("Retrieved {Count} leaderboard entries for guild {GuildId}", result.Count(), guildId);
        return result;
    }
}
