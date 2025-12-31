using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for ModTag entities with tag-specific operations.
/// </summary>
public class ModTagRepository : Repository<ModTag>, IModTagRepository
{
    private readonly ILogger<ModTagRepository> _logger;

    public ModTagRepository(
        BotDbContext context,
        ILogger<ModTagRepository> logger,
        ILogger<Repository<ModTag>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Overrides base implementation to include Guild and UserTags navigation properties.
    /// </remarks>
    public override async Task<ModTag?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving mod tag by ID: {Id}", id);

        if (id is not Guid guidId)
        {
            _logger.LogWarning("Invalid ID type for ModTag: {IdType}", id?.GetType().Name ?? "null");
            return null;
        }

        var result = await DbSet
            .AsNoTracking()
            .Include(t => t.Guild)
            .Include(t => t.UserTags)
            .FirstOrDefaultAsync(t => t.Id == guidId, cancellationToken);

        _logger.LogDebug("Mod tag {Id} found: {Found}", id, result != null);
        return result;
    }

    public async Task<IEnumerable<ModTag>> GetByGuildAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving mod tags for guild {GuildId}", guildId);

        var results = await DbSet
            .AsNoTracking()
            .Include(t => t.Guild)
            .Where(t => t.GuildId == guildId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} mod tags for guild {GuildId}", results.Count, guildId);
        return results;
    }

    public async Task<ModTag?> GetByNameAsync(
        ulong guildId,
        string name,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving mod tag {Name} for guild {GuildId}", name, guildId);

        var result = await DbSet
            .AsNoTracking()
            .Include(t => t.Guild)
            .Include(t => t.UserTags)
            .FirstOrDefaultAsync(t => t.GuildId == guildId && t.Name.ToLower() == name.ToLower(), cancellationToken);

        _logger.LogDebug("Mod tag {Name} for guild {GuildId} found: {Found}", name, guildId, result != null);
        return result;
    }

    public async Task<bool> NameExistsAsync(
        ulong guildId,
        string name,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking if mod tag name {Name} exists in guild {GuildId}", name, guildId);

        var exists = await DbSet
            .AsNoTracking()
            .AnyAsync(t => t.GuildId == guildId && t.Name.ToLower() == name.ToLower(), cancellationToken);

        _logger.LogDebug("Mod tag name {Name} exists in guild {GuildId}: {Exists}", name, guildId, exists);
        return exists;
    }
}
