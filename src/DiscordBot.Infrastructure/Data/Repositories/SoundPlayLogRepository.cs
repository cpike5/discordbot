using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for SoundPlayLog entities with time-series querying and batch deletion.
/// </summary>
public class SoundPlayLogRepository : ISoundPlayLogRepository
{
    private readonly BotDbContext _context;
    private readonly ILogger<SoundPlayLogRepository> _logger;

    public SoundPlayLogRepository(BotDbContext context, ILogger<SoundPlayLogRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task AddAsync(SoundPlayLog log, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Adding sound play log - SoundId: {SoundId}, GuildId: {GuildId}, UserId: {UserId}",
            log.SoundId, log.GuildId, log.UserId);

        _context.SoundPlayLogs.Add(log);
        await _context.SaveChangesAsync(ct);

        _logger.LogDebug("Sound play log added with Id: {Id}", log.Id);
    }

    /// <inheritdoc/>
    public async Task<int> GetPlayCountAsync(ulong guildId, DateTime since, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting play count for guild {GuildId} since {Since}",
            guildId, since);

        var count = await _context.SoundPlayLogs
            .AsNoTracking()
            .Where(l => l.GuildId == guildId && l.PlayedAt >= since)
            .CountAsync(ct);

        _logger.LogDebug(
            "Guild {GuildId} has {Count} plays since {Since}",
            guildId, count, since);

        return count;
    }

    /// <inheritdoc/>
    public async Task<int> GetPlayCountForSoundAsync(Guid soundId, DateTime since, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting play count for sound {SoundId} since {Since}",
            soundId, since);

        var count = await _context.SoundPlayLogs
            .AsNoTracking()
            .Where(l => l.SoundId == soundId && l.PlayedAt >= since)
            .CountAsync(ct);

        _logger.LogDebug(
            "Sound {SoundId} has {Count} plays since {Since}",
            soundId, count, since);

        return count;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, int batchSize, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Deleting sound play logs older than {Cutoff} (batch size: {BatchSize})",
            cutoff, batchSize);

        // Get the IDs of records to delete (using PlayedAt index for efficiency)
        var idsToDelete = await _context.SoundPlayLogs
            .AsNoTracking()
            .Where(l => l.PlayedAt < cutoff)
            .OrderBy(l => l.PlayedAt)
            .Take(batchSize)
            .Select(l => l.Id)
            .ToListAsync(ct);

        if (idsToDelete.Count == 0)
        {
            _logger.LogDebug("No sound play logs found older than {Cutoff}", cutoff);
            return 0;
        }

        // Delete the batch
        var deleted = await _context.SoundPlayLogs
            .Where(l => idsToDelete.Contains(l.Id))
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation(
            "Deleted {Count} sound play logs older than {Cutoff}",
            deleted, cutoff);

        return deleted;
    }
}
