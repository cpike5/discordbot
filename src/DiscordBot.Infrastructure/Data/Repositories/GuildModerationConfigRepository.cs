using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for GuildModerationConfig entities with configuration-specific operations.
/// </summary>
public class GuildModerationConfigRepository : Repository<GuildModerationConfig>, IGuildModerationConfigRepository
{
    private readonly ILogger<GuildModerationConfigRepository> _logger;

    public GuildModerationConfigRepository(
        BotDbContext context,
        ILogger<GuildModerationConfigRepository> logger,
        ILogger<Repository<GuildModerationConfig>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Overrides base implementation to include Guild navigation property.
    /// GuildId is the primary key for this entity.
    /// </remarks>
    public override async Task<GuildModerationConfig?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving guild moderation config by ID: {Id}", id);

        if (id is not ulong guildId)
        {
            _logger.LogWarning("Invalid ID type for GuildModerationConfig: {IdType}", id?.GetType().Name ?? "null");
            return null;
        }

        return await GetByGuildIdAsync(guildId, cancellationToken);
    }

    public async Task<GuildModerationConfig?> GetByGuildIdAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving moderation config for guild {GuildId}", guildId);

        var result = await DbSet
            .AsNoTracking()
            .Include(c => c.Guild)
            .FirstOrDefaultAsync(c => c.GuildId == guildId, cancellationToken);

        _logger.LogDebug("Moderation config for guild {GuildId} found: {Found}", guildId, result != null);
        return result;
    }
}
