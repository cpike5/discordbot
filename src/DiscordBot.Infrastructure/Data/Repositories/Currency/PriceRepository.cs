using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for <see cref="PriceEntry"/>.
/// </summary>
public class PriceRepository : Repository<PriceEntry>, IPriceRepository
{
    private readonly ILogger<PriceRepository> _logger;

    public PriceRepository(
        BotDbContext context,
        ILogger<PriceRepository> logger,
        ILogger<Repository<PriceEntry>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PriceEntry?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet.Include(p => p.Currency).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PriceEntry?> GetActiveAsync(string featureKey, ulong? guildId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            return null;
        }

        if (guildId.HasValue)
        {
            var guildPrice = await DbSet.AsNoTracking()
                .Include(p => p.Currency)
                .FirstOrDefaultAsync(p => p.FeatureKey == featureKey && p.GuildId == guildId && p.IsActive, cancellationToken);

            if (guildPrice != null)
            {
                return guildPrice;
            }
        }

        // Fall back to the entry that applies everywhere.
        return await DbSet.AsNoTracking()
            .Include(p => p.Currency)
            .FirstOrDefaultAsync(p => p.FeatureKey == featureKey && p.GuildId == null && p.IsActive, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PriceEntry?> GetByFeatureAsync(string featureKey, ulong? guildId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            return null;
        }

        return await DbSet
            .Include(p => p.Currency)
            .FirstOrDefaultAsync(p => p.FeatureKey == featureKey && p.GuildId == guildId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PriceEntry>> GetForGuildAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching price entries for guild {GuildId}", guildId);

        return await DbSet.AsNoTracking()
            .Include(p => p.Currency)
            .Where(p => p.GuildId == guildId || p.GuildId == null)
            .OrderBy(p => p.FeatureKey)
            .ToListAsync(cancellationToken);
    }
}
