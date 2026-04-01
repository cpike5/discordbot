using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for NotXGuildSettings entities with settings-specific operations.
/// </summary>
public class NotXGuildSettingsRepository : Repository<NotXGuildSettings>, INotXGuildSettingsRepository
{
    private readonly ILogger<NotXGuildSettingsRepository> _logger;

    public NotXGuildSettingsRepository(
        BotDbContext context,
        ILogger<NotXGuildSettingsRepository> logger,
        ILogger<Repository<NotXGuildSettings>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<NotXGuildSettings?> GetByGuildIdAsync(ulong guildId)
    {
        _logger.LogDebug("Retrieving not-X settings for guild {GuildId}", guildId);

        var settings = await DbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.GuildId == guildId);

        _logger.LogDebug("Settings found for guild {GuildId}: {Found}", guildId, settings != null);
        return settings;
    }

    public async Task<NotXGuildSettings> GetOrCreateAsync(ulong guildId)
    {
        _logger.LogDebug("Getting or creating not-X settings for guild {GuildId}", guildId);

        var settings = await DbSet
            .FirstOrDefaultAsync(s => s.GuildId == guildId);

        if (settings != null)
        {
            _logger.LogDebug("Existing not-X settings found for guild {GuildId}", guildId);
            return settings;
        }

        _logger.LogInformation("Creating default not-X settings for guild {GuildId}", guildId);

        var now = DateTime.UtcNow;
        settings = new NotXGuildSettings
        {
            GuildId = guildId,
            IsEnabled = false,
            SensitiveOnly = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await DbSet.AddAsync(settings);
        await Context.SaveChangesAsync();

        _logger.LogInformation("Created default not-X settings for guild {GuildId}", guildId);

        return settings;
    }
}
