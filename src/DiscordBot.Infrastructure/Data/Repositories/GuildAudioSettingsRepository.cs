using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for GuildAudioSettings entities with settings-specific operations.
/// </summary>
public class GuildAudioSettingsRepository : Repository<GuildAudioSettings>, IGuildAudioSettingsRepository
{
    private readonly ILogger<GuildAudioSettingsRepository> _logger;

    public GuildAudioSettingsRepository(
        BotDbContext context,
        ILogger<GuildAudioSettingsRepository> logger,
        ILogger<Repository<GuildAudioSettings>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<GuildAudioSettings?> GetByGuildIdAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving audio settings for guild {GuildId}", guildId);

        var settings = await DbSet
            .AsNoTracking()
            .Include(s => s.Guild)
            .Include(s => s.CommandRoleRestrictions)
            .FirstOrDefaultAsync(s => s.GuildId == guildId, cancellationToken);

        _logger.LogDebug("Audio settings found for guild {GuildId}: {Found}", guildId, settings != null);
        return settings;
    }

    public async Task<GuildAudioSettings> GetOrCreateAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting or creating audio settings for guild {GuildId}", guildId);

        var settings = await DbSet
            .Include(s => s.Guild)
            .Include(s => s.CommandRoleRestrictions)
            .FirstOrDefaultAsync(s => s.GuildId == guildId, cancellationToken);

        if (settings != null)
        {
            _logger.LogDebug("Existing audio settings found for guild {GuildId}", guildId);
            return settings;
        }

        _logger.LogInformation("Creating default audio settings for guild {GuildId}", guildId);

        var now = DateTime.UtcNow;
        settings = new GuildAudioSettings
        {
            GuildId = guildId,
            AudioEnabled = true,
            AutoLeaveTimeoutMinutes = 5,
            QueueEnabled = true,
            MaxDurationSeconds = 30,
            MaxFileSizeBytes = 5_242_880,
            MaxSoundsPerGuild = 50,
            MaxStorageBytes = 104_857_600,
            CreatedAt = now,
            UpdatedAt = now
        };

        await DbSet.AddAsync(settings, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created default audio settings for guild {GuildId}",
            guildId);

        return settings;
    }
}
