using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for AudioPlaybackLog entities with paged querying and filtering.
/// </summary>
public class AudioPlaybackLogRepository : Repository<AudioPlaybackLog>, IAudioPlaybackLogRepository
{
    public AudioPlaybackLogRepository(BotDbContext context, ILogger<Repository<AudioPlaybackLog>> logger)
        : base(context, logger)
    {
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<AudioPlaybackLog> Items, int TotalCount)> GetPagedAsync(
        ulong guildId,
        int page,
        int pageSize,
        AudioFeatureType? featureType,
        ulong? userId,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default)
    {
        var query = DbSet
            .AsNoTracking()
            .Where(a => a.GuildId == guildId);

        // Apply optional filters
        if (featureType.HasValue)
        {
            query = query.Where(a => a.FeatureType == featureType.Value);
        }

        if (userId.HasValue)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(a => a.PlayedAt >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(a => a.PlayedAt <= to.Value);
        }

        // Order by most recent first
        query = query.OrderByDescending(a => a.PlayedAt);

        return await GetPagedAsync(query, page, pageSize, ct);
    }

    /// <inheritdoc/>
    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, int batchSize, CancellationToken ct = default)
    {
        // Clamped to 1000 - the ids selected below become an IN (...) list. See
        // LlmUsageRepository.DeleteOlderThanAsync for why 1000 is the ceiling.
        batchSize = Math.Clamp(batchSize, 1, 1000);

        var idsToDelete = await DbSet
            .AsNoTracking()
            .Where(a => a.PlayedAt < cutoff)
            .OrderBy(a => a.Id)
            .Select(a => a.Id)
            .Take(batchSize)
            .ToListAsync(ct);

        if (idsToDelete.Count == 0)
        {
            return 0;
        }

        return await DbSet
            .Where(a => idsToDelete.Contains(a.Id))
            .ExecuteDeleteAsync(ct);
    }
}
