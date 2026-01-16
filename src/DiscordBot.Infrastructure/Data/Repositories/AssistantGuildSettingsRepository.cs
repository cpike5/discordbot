using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for AssistantGuildSettings entities.
/// Provides data access operations for assistant configuration per guild.
/// </summary>
public class AssistantGuildSettingsRepository : Repository<AssistantGuildSettings>, IAssistantGuildSettingsRepository
{
    private readonly ILogger<AssistantGuildSettingsRepository> _logger;

    public AssistantGuildSettingsRepository(
        BotDbContext context,
        ILogger<AssistantGuildSettingsRepository> logger,
        ILogger<Repository<AssistantGuildSettings>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AssistantGuildSettings?> GetByGuildIdAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving assistant settings for guild {GuildId}", guildId);

        var settings = await DbSet
            .AsNoTracking()
            .Include(s => s.Guild)
            .FirstOrDefaultAsync(s => s.GuildId == guildId, cancellationToken);

        _logger.LogDebug("Settings found for guild {GuildId}: {Found}", guildId, settings != null);
        return settings;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<AssistantGuildSettings>> GetEnabledGuildsAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all guilds with assistant enabled");

        var settings = await DbSet
            .AsNoTracking()
            .Include(s => s.Guild)
            .Where(s => s.IsEnabled)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} guilds with assistant enabled", settings.Count);
        return settings;
    }
}
