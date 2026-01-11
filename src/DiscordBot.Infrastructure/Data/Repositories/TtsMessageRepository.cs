using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for TtsMessage entities with TTS-specific operations.
/// Provides methods for logging TTS messages and querying usage statistics.
/// </summary>
public class TtsMessageRepository : ITtsMessageRepository
{
    private readonly BotDbContext _context;
    private readonly ILogger<TtsMessageRepository> _logger;

    public TtsMessageRepository(BotDbContext context, ILogger<TtsMessageRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<TtsMessage> AddAsync(TtsMessage message, CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Adding TTS message - GuildId: {GuildId}, UserId: {UserId}, MessageLength: {Length}",
            message.GuildId, message.UserId, message.Message.Length);

        _context.TtsMessages.Add(message);
        await _context.SaveChangesAsync(ct);

        _logger.LogDebug("TTS message added with Id: {Id}", message.Id);
        return message;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TtsMessage>> GetRecentByGuildAsync(
        ulong guildId,
        int count = 20,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting recent TTS messages for guild {GuildId}, count: {Count}",
            guildId, count);

        var messages = await _context.TtsMessages
            .AsNoTracking()
            .Where(m => m.GuildId == guildId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(count)
            .ToListAsync(ct);

        _logger.LogDebug(
            "Retrieved {Count} TTS messages for guild {GuildId}",
            messages.Count, guildId);

        return messages;
    }

    /// <inheritdoc/>
    public async Task<int> GetMessageCountAsync(
        ulong guildId,
        DateTime since,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting TTS message count for guild {GuildId} since {Since}",
            guildId, since);

        var count = await _context.TtsMessages
            .AsNoTracking()
            .Where(m => m.GuildId == guildId && m.CreatedAt >= since)
            .CountAsync(ct);

        _logger.LogDebug(
            "Guild {GuildId} has {Count} TTS messages since {Since}",
            guildId, count, since);

        return count;
    }

    /// <inheritdoc/>
    public async Task<double> GetTotalPlaybackSecondsAsync(
        ulong guildId,
        DateTime since,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting total playback seconds for guild {GuildId} since {Since}",
            guildId, since);

        var totalSeconds = await _context.TtsMessages
            .AsNoTracking()
            .Where(m => m.GuildId == guildId && m.CreatedAt >= since)
            .SumAsync(m => m.DurationSeconds, ct);

        _logger.LogDebug(
            "Guild {GuildId} has {TotalSeconds} seconds of TTS playback since {Since}",
            guildId, totalSeconds, since);

        return totalSeconds;
    }

    /// <inheritdoc/>
    public async Task<int> GetUniqueUserCountAsync(
        ulong guildId,
        DateTime since,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting unique user count for guild {GuildId} since {Since}",
            guildId, since);

        var count = await _context.TtsMessages
            .AsNoTracking()
            .Where(m => m.GuildId == guildId && m.CreatedAt >= since)
            .Select(m => m.UserId)
            .Distinct()
            .CountAsync(ct);

        _logger.LogDebug(
            "Guild {GuildId} has {Count} unique TTS users since {Since}",
            guildId, count, since);

        return count;
    }

    /// <inheritdoc/>
    public async Task<string?> GetMostUsedVoiceAsync(
        ulong guildId,
        DateTime since,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting most used voice for guild {GuildId} since {Since}",
            guildId, since);

        var voice = await _context.TtsMessages
            .AsNoTracking()
            .Where(m => m.GuildId == guildId && m.CreatedAt >= since && !string.IsNullOrEmpty(m.Voice))
            .GroupBy(m => m.Voice)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefaultAsync(ct);

        _logger.LogDebug(
            "Most used voice for guild {GuildId}: {Voice}",
            guildId, voice ?? "(none)");

        return voice;
    }

    /// <inheritdoc/>
    public async Task<(ulong UserId, string Username, int MessageCount)?> GetTopUserAsync(
        ulong guildId,
        DateTime since,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting top TTS user for guild {GuildId} since {Since}",
            guildId, since);

        var topUser = await _context.TtsMessages
            .AsNoTracking()
            .Where(m => m.GuildId == guildId && m.CreatedAt >= since)
            .GroupBy(m => new { m.UserId, m.Username })
            .Select(g => new
            {
                g.Key.UserId,
                g.Key.Username,
                MessageCount = g.Count()
            })
            .OrderByDescending(x => x.MessageCount)
            .FirstOrDefaultAsync(ct);

        if (topUser == null)
        {
            _logger.LogDebug("No TTS messages found for guild {GuildId} since {Since}", guildId, since);
            return null;
        }

        _logger.LogDebug(
            "Top TTS user for guild {GuildId}: {Username} ({UserId}) with {Count} messages",
            guildId, topUser.Username, topUser.UserId, topUser.MessageCount);

        return (topUser.UserId, topUser.Username, topUser.MessageCount);
    }

    /// <inheritdoc/>
    public async Task<int> GetUserMessageCountAsync(
        ulong guildId,
        ulong userId,
        DateTime since,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting TTS message count for user {UserId} in guild {GuildId} since {Since}",
            userId, guildId, since);

        var count = await _context.TtsMessages
            .AsNoTracking()
            .Where(m => m.GuildId == guildId && m.UserId == userId && m.CreatedAt >= since)
            .CountAsync(ct);

        _logger.LogDebug(
            "User {UserId} has {Count} TTS messages in guild {GuildId} since {Since}",
            userId, count, guildId, since);

        return count;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogDebug("Deleting TTS message with Id: {Id}", id);

        var message = await _context.TtsMessages.FindAsync(new object[] { id }, ct);
        if (message == null)
        {
            _logger.LogDebug("TTS message with Id {Id} not found", id);
            return false;
        }

        _context.TtsMessages.Remove(message);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted TTS message with Id: {Id}", id);
        return true;
    }

    /// <inheritdoc/>
    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, int batchSize, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Deleting TTS messages older than {Cutoff} (batch size: {BatchSize})",
            cutoff, batchSize);

        // Get the IDs of records to delete
        var idsToDelete = await _context.TtsMessages
            .AsNoTracking()
            .Where(m => m.CreatedAt < cutoff)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .Select(m => m.Id)
            .ToListAsync(ct);

        if (idsToDelete.Count == 0)
        {
            _logger.LogDebug("No TTS messages found older than {Cutoff}", cutoff);
            return 0;
        }

        // Delete the batch
        var deleted = await _context.TtsMessages
            .Where(m => idsToDelete.Contains(m.Id))
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation(
            "Deleted {Count} TTS messages older than {Cutoff}",
            deleted, cutoff);

        return deleted;
    }
}
