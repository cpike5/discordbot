using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for <see cref="Currency"/>.
/// </summary>
public class CurrencyRepository : Repository<Currency>, ICurrencyRepository
{
    private readonly ILogger<CurrencyRepository> _logger;

    public CurrencyRepository(
        BotDbContext context,
        ILogger<CurrencyRepository> logger,
        ILogger<Repository<Currency>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Currency?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Currency>> GetVisibleInGuildAsync(
        ulong guildId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .Where(c => c.Scope == CurrencyScope.Global || c.GuildId == guildId);

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Scope)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Currency>> GetForGuildAsync(
        ulong guildId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .Where(c => c.Scope == CurrencyScope.Guild && c.GuildId == guildId);

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Currency>> GetGlobalAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking().Where(c => c.Scope == CurrencyScope.Global);

        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query.OrderBy(c => c.Name).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> NameExistsAsync(
        CurrencyScope scope,
        ulong? guildId,
        string name,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // Case-insensitive without a provider-specific collation: both providers translate
        // ToLower() to their own lower() function.
        var normalized = name.Trim().ToLowerInvariant();

        var query = DbSet.AsNoTracking()
            .Where(c => c.Scope == scope && c.GuildId == guildId && c.Name.ToLower() == normalized);

        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        var exists = await query.AnyAsync(cancellationToken);

        if (exists)
        {
            _logger.LogDebug("Currency name {Name} already exists in scope {Scope} for guild {GuildId}", name, scope, guildId);
        }

        return exists;
    }
}
