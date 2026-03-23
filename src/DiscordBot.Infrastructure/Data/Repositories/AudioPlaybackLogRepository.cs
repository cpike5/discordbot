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
}
