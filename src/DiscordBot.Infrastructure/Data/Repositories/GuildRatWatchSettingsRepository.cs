using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for GuildRatWatchSettings entities with settings-specific operations.
/// </summary>
public class GuildRatWatchSettingsRepository : Repository<GuildRatWatchSettings>, IGuildRatWatchSettingsRepository
{
    private readonly ILogger<GuildRatWatchSettingsRepository> _logger;

    public GuildRatWatchSettingsRepository(
        BotDbContext context,
        ILogger<GuildRatWatchSettingsRepository> logger,
        ILogger<Repository<GuildRatWatchSettings>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<GuildRatWatchSettings?> GetByGuildIdAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving Rat Watch settings for guild {GuildId}", guildId);

        var settings = await DbSet
            .AsNoTracking()
            .Include(s => s.Guild)
            .FirstOrDefaultAsync(s => s.GuildId == guildId, cancellationToken);

        _logger.LogDebug("Settings found for guild {GuildId}: {Found}", guildId, settings != null);
        return settings;
    }

    public async Task<GuildRatWatchSettings> GetOrCreateAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting or creating Rat Watch settings for guild {GuildId}", guildId);

        var settings = await DbSet
            .Include(s => s.Guild)
            .FirstOrDefaultAsync(s => s.GuildId == guildId, cancellationToken);

        if (settings != null)
        {
            _logger.LogDebug("Existing settings found for guild {GuildId}", guildId);
            return settings;
        }

        _logger.LogInformation("Creating default Rat Watch settings for guild {GuildId}", guildId);

        var now = DateTime.UtcNow;
        settings = new GuildRatWatchSettings
        {
            GuildId = guildId,
            IsEnabled = true,
            Timezone = "Eastern Standard Time",
            MaxAdvanceHours = 24,
            VotingDurationMinutes = 5,
            CreatedAt = now,
            UpdatedAt = now
        };

        await DbSet.AddAsync(settings, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created default Rat Watch settings for guild {GuildId} with ID {GuildId}",
            guildId, guildId);

        return settings;
    }

    public async Task<Dictionary<ulong, int>> GetVotingDurationsForGuildsAsync(
        IEnumerable<ulong> guildIds,
        CancellationToken cancellationToken = default)
    {
        var guildIdList = guildIds.ToList();
        if (guildIdList.Count == 0)
        {
            _logger.LogDebug("GetVotingDurationsForGuildsAsync called with empty guild list");
            return new Dictionary<ulong, int>();
        }

        _logger.LogDebug(
            "Retrieving voting durations for {Count} guilds",
            guildIdList.Count);

        var settings = await DbSet
            .AsNoTracking()
            .Where(s => guildIdList.Contains(s.GuildId))
            .Select(s => new { s.GuildId, s.VotingDurationMinutes })
            .ToListAsync(cancellationToken);

        var result = settings.ToDictionary(s => s.GuildId, s => s.VotingDurationMinutes);

        _logger.LogDebug(
            "Retrieved voting durations for {Count} guilds (requested {Requested})",
            result.Count, guildIdList.Count);

        return result;
    }
}
