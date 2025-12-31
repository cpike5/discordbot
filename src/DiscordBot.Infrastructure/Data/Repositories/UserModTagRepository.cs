using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for UserModTag entities with tag assignment operations.
/// </summary>
public class UserModTagRepository : Repository<UserModTag>, IUserModTagRepository
{
    private readonly ILogger<UserModTagRepository> _logger;

    public UserModTagRepository(
        BotDbContext context,
        ILogger<UserModTagRepository> logger,
        ILogger<Repository<UserModTag>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Overrides base implementation to include Tag navigation property.
    /// </remarks>
    public override async Task<UserModTag?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving user mod tag by ID: {Id}", id);

        if (id is not Guid guidId)
        {
            _logger.LogWarning("Invalid ID type for UserModTag: {IdType}", id?.GetType().Name ?? "null");
            return null;
        }

        var result = await DbSet
            .AsNoTracking()
            .Include(ut => ut.Tag)
            .FirstOrDefaultAsync(ut => ut.Id == guidId, cancellationToken);

        _logger.LogDebug("User mod tag {Id} found: {Found}", id, result != null);
        return result;
    }

    public async Task<IEnumerable<UserModTag>> GetByUserAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving mod tags for user {UserId} in guild {GuildId}", userId, guildId);

        var results = await DbSet
            .AsNoTracking()
            .Include(ut => ut.Tag)
            .Where(ut => ut.GuildId == guildId && ut.UserId == userId)
            .OrderBy(ut => ut.AppliedAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Retrieved {Count} mod tags for user {UserId}", results.Count, userId);
        return results;
    }

    public async Task<bool> ExistsAsync(
        ulong guildId,
        ulong userId,
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Checking if tag {TagId} is assigned to user {UserId} in guild {GuildId}", tagId, userId, guildId);

        var exists = await DbSet
            .AsNoTracking()
            .AnyAsync(ut => ut.GuildId == guildId && ut.UserId == userId && ut.TagId == tagId, cancellationToken);

        _logger.LogDebug("Tag {TagId} assigned to user {UserId} in guild {GuildId}: {Exists}", tagId, userId, guildId, exists);
        return exists;
    }

    public async Task<UserModTag?> GetAssignmentAsync(
        ulong guildId,
        ulong userId,
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving tag assignment {TagId} for user {UserId} in guild {GuildId}", tagId, userId, guildId);

        var result = await DbSet
            .AsNoTracking()
            .Include(ut => ut.Tag)
            .FirstOrDefaultAsync(ut => ut.GuildId == guildId && ut.UserId == userId && ut.TagId == tagId, cancellationToken);

        _logger.LogDebug("Tag assignment {TagId} for user {UserId} found: {Found}", tagId, userId, result != null);
        return result;
    }
}
