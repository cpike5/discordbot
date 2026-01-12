using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for GuildTtsSettings entities with settings-specific operations.
/// </summary>
public class GuildTtsSettingsRepository : Repository<GuildTtsSettings>, IGuildTtsSettingsRepository
{
    private readonly ILogger<GuildTtsSettingsRepository> _logger;

    public GuildTtsSettingsRepository(
        BotDbContext context,
        ILogger<GuildTtsSettingsRepository> logger,
        ILogger<Repository<GuildTtsSettings>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<GuildTtsSettings?> GetByGuildIdAsync(
        ulong guildId,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Retrieving TTS settings for guild {GuildId}", guildId);

        var settings = await DbSet
            .AsNoTracking()
            .Include(s => s.Guild)
            .FirstOrDefaultAsync(s => s.GuildId == guildId, ct);

        _logger.LogDebug("TTS settings found for guild {GuildId}: {Found}", guildId, settings != null);
        return settings;
    }

    /// <inheritdoc/>
    public async Task<GuildTtsSettings> GetOrCreateAsync(
        ulong guildId,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting or creating TTS settings for guild {GuildId}", guildId);

        var settings = await DbSet
            .Include(s => s.Guild)
            .FirstOrDefaultAsync(s => s.GuildId == guildId, ct);

        if (settings != null)
        {
            _logger.LogDebug("Existing TTS settings found for guild {GuildId}", guildId);
            return settings;
        }

        _logger.LogInformation("Creating default TTS settings for guild {GuildId}", guildId);

        var now = DateTime.UtcNow;
        settings = new GuildTtsSettings
        {
            GuildId = guildId,
            TtsEnabled = true,
            DefaultVoice = "en-US-JennyNeural",
            DefaultSpeed = 1.0,
            DefaultPitch = 1.0,
            DefaultVolume = 0.8,
            MaxMessageLength = 500,
            RateLimitPerMinute = 5,
            AutoPlayOnSend = false,
            AnnounceJoinsLeaves = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        await DbSet.AddAsync(settings, ct);
        await Context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created default TTS settings for guild {GuildId}",
            guildId);

        return settings;
    }
}
