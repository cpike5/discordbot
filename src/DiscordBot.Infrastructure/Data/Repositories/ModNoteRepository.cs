using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for ModNote entities with note-specific operations.
/// </summary>
public class ModNoteRepository : Repository<ModNote>, IModNoteRepository
{
    private readonly ILogger<ModNoteRepository> _logger;

    public ModNoteRepository(
        BotDbContext context,
        ILogger<ModNoteRepository> logger,
        ILogger<Repository<ModNote>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Overrides base implementation to include Guild navigation property.
    /// </remarks>
    public override async Task<ModNote?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving mod note by ID: {Id}", id);

        if (id is not Guid guidId)
        {
            _logger.LogWarning("Invalid ID type for ModNote: {IdType}", id?.GetType().Name ?? "null");
            return null;
        }

        var result = await DbSet
            .AsNoTracking()
            .Include(n => n.Guild)
            .FirstOrDefaultAsync(n => n.Id == guidId, cancellationToken);

        _logger.LogDebug("Mod note {Id} found: {Found}", id, result != null);
        return result;
    }

    public async Task<IEnumerable<ModNote>> GetByUserAsync(
        ulong guildId,
        ulong targetUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving mod notes for user {UserId} in guild {GuildId}", targetUserId, guildId);

        var results = await DbSet
            .AsNoTracking()
            .Include(n => n.Guild)
            .Where(n => n.GuildId == guildId && n.TargetUserId == targetUserId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} mod notes for user {UserId}", results.Count, targetUserId);
        return results;
    }

    public async Task<IEnumerable<ModNote>> GetByAuthorAsync(
        ulong guildId,
        ulong authorUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving mod notes by author {AuthorUserId} in guild {GuildId}", authorUserId, guildId);

        var results = await DbSet
            .AsNoTracking()
            .Include(n => n.Guild)
            .Where(n => n.GuildId == guildId && n.AuthorUserId == authorUserId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} mod notes by author {AuthorUserId}", results.Count, authorUserId);
        return results;
    }
}
