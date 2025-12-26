using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for WelcomeConfiguration entities with Discord-specific operations.
/// </summary>
public class WelcomeConfigurationRepository : Repository<WelcomeConfiguration>, IWelcomeConfigurationRepository
{
    private readonly ILogger<WelcomeConfigurationRepository> _logger;

    public WelcomeConfigurationRepository(
        BotDbContext context,
        ILogger<WelcomeConfigurationRepository> logger,
        ILogger<Repository<WelcomeConfiguration>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<WelcomeConfiguration?> GetByGuildIdAsync(ulong guildId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving welcome configuration for guild {GuildId}", guildId);
        // Use FirstOrDefaultAsync instead of FindAsync to ensure proper value conversion for ulong keys
        return await DbSet.FirstOrDefaultAsync(w => w.GuildId == guildId, cancellationToken);
    }
}
