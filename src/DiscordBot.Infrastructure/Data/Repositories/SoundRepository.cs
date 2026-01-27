using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for Sound entities with soundboard-specific operations.
/// </summary>
public class SoundRepository : Repository<Sound>, ISoundRepository
{
    private readonly ILogger<SoundRepository> _logger;

    public SoundRepository(
        BotDbContext context,
        ILogger<SoundRepository> logger,
        ILogger<Repository<Sound>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<Sound>> GetByGuildIdAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving all sounds for guild {GuildId}", guildId);

        var sounds = await DbSet
            .AsNoTracking()
            .Where(s => s.GuildId == guildId)
            .Include(s => s.Guild)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} sounds for guild {GuildId}", sounds.Count, guildId);
        return sounds;
    }

    public async Task<Sound?> GetByIdAndGuildAsync(
        Guid id,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving sound {SoundId} for guild {GuildId}", id, guildId);

        var sound = await DbSet
            .AsNoTracking()
            .Where(s => s.Id == id && s.GuildId == guildId)
            .Include(s => s.Guild)
            .FirstOrDefaultAsync(cancellationToken);

        _logger.LogDebug("Sound {SoundId} found for guild {GuildId}: {Found}", id, guildId, sound != null);
        return sound;
    }

    public async Task<Sound?> GetByNameAndGuildAsync(
        string name,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving sound by name '{Name}' for guild {GuildId}", name, guildId);

        var sound = await DbSet
            .AsNoTracking()
            .Where(s => s.GuildId == guildId)
            .FirstOrDefaultAsync(s => s.Name.ToLower() == name.ToLower(), cancellationToken);

        _logger.LogDebug("Sound '{Name}' found for guild {GuildId}: {Found}", name, guildId, sound != null);
        return sound;
    }

    public async Task<long> GetTotalStorageUsedAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Calculating total storage used for guild {GuildId}", guildId);

        var totalBytes = await DbSet
            .Where(s => s.GuildId == guildId)
            .SumAsync(s => s.FileSizeBytes, cancellationToken);

        _logger.LogDebug("Total storage used for guild {GuildId}: {TotalBytes} bytes", guildId, totalBytes);
        return totalBytes;
    }

    public async Task<int> GetSoundCountAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Counting sounds for guild {GuildId}", guildId);

        var count = await DbSet
            .CountAsync(s => s.GuildId == guildId, cancellationToken);

        _logger.LogDebug("Sound count for guild {GuildId}: {Count}", guildId, count);
        return count;
    }

    public async Task IncrementPlayCountAsync(
        Guid soundId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Incrementing play count for sound {SoundId}", soundId);

        var sound = await DbSet.FirstOrDefaultAsync(s => s.Id == soundId, cancellationToken);
        if (sound == null)
        {
            _logger.LogWarning("Cannot increment play count: sound {SoundId} not found", soundId);
            return;
        }

        sound.PlayCount++;
        await Context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Incremented play count for sound {SoundId} to {PlayCount}", soundId, sound.PlayCount);
    }

    public async Task<IReadOnlyList<(string Name, int PlayCount)>> GetTopSoundsByPlayCountAsync(
        ulong guildId,
        int count,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving top {Count} sounds by play count for guild {GuildId} since {Since}",
            count, guildId, since);

        var topSounds = await Context.SoundPlayLogs
            .AsNoTracking()
            .Include(log => log.Sound)
            .Where(log => log.GuildId == guildId && log.PlayedAt >= since && log.Sound != null)
            .GroupBy(log => new { log.SoundId, log.Sound.Name })
            .Select(g => new { g.Key.Name, PlayCount = g.Count() })
            .OrderByDescending(x => x.PlayCount)
            .Take(count)
            .ToListAsync(cancellationToken);

        var result = topSounds.Select(x => (x.Name, x.PlayCount)).ToList();

        _logger.LogDebug("Found {Count} top sounds for guild {GuildId} since {Since}",
            result.Count, guildId, since);

        return result;
    }
}
