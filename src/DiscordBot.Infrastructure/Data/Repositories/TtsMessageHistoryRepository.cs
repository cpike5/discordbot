using System.Diagnostics;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Tracing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for TtsMessageHistory entities with history-specific operations.
/// </summary>
public class TtsMessageHistoryRepository : Repository<TtsMessageHistory>, ITtsMessageHistoryRepository
{
    private readonly ILogger<TtsMessageHistoryRepository> _logger;
    private const int SlowOperationThresholdMs = 100;

    public TtsMessageHistoryRepository(
        BotDbContext context,
        ILogger<TtsMessageHistoryRepository> logger,
        ILogger<Repository<TtsMessageHistory>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<TtsMessageHistory>> GetRecentAsync(
        ulong userId,
        ulong guildId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetRecentAsync",
            entityType: nameof(TtsMessageHistory),
            dbOperation: "SELECT",
            entityId: $"{userId}:{guildId}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Retrieving recent TTS history for user {UserId} in guild {GuildId}, limit {Limit}",
            userId, guildId, limit);

        try
        {
            var entries = await DbSet
                .AsNoTracking()
                .Where(h => h.UserId == userId && h.GuildId == guildId)
                .OrderByDescending(h => h.PlayedAt)
                .Take(limit)
                .ToListAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug(
                "Retrieved {Count} recent TTS history entries for user {UserId} in guild {GuildId} in {ElapsedMs}ms",
                entries.Count, userId, guildId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "TtsMessageHistoryRepository.GetRecentAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, UserId={UserId}, GuildId={GuildId}, Count={Count}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, userId, guildId, entries.Count);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return entries;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to retrieve recent TTS history for user {UserId} in guild {GuildId}. ElapsedMs={ElapsedMs}, Error={Error}",
                userId, guildId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task<IReadOnlyList<TtsMessageHistory>> GetFavoritesAsync(
        ulong userId,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetFavoritesAsync",
            entityType: nameof(TtsMessageHistory),
            dbOperation: "SELECT",
            entityId: $"{userId}:{guildId}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Retrieving favorite TTS messages for user {UserId} in guild {GuildId}",
            userId, guildId);

        try
        {
            var entries = await DbSet
                .AsNoTracking()
                .Where(h => h.UserId == userId && h.GuildId == guildId && h.IsFavorite)
                .OrderByDescending(h => h.PlayedAt)
                .ToListAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug(
                "Retrieved {Count} favorite TTS messages for user {UserId} in guild {GuildId} in {ElapsedMs}ms",
                entries.Count, userId, guildId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "TtsMessageHistoryRepository.GetFavoritesAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, UserId={UserId}, GuildId={GuildId}, Count={Count}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, userId, guildId, entries.Count);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return entries;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to retrieve favorite TTS messages for user {UserId} in guild {GuildId}. ElapsedMs={ElapsedMs}, Error={Error}",
                userId, guildId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task SetFavoriteAsync(
        int id,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "SetFavoriteAsync",
            entityType: nameof(TtsMessageHistory),
            dbOperation: "UPDATE",
            entityId: id.ToString());

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Setting favorite status for TTS history entry {Id} to {IsFavorite}",
            id, isFavorite);

        try
        {
            var entry = await DbSet.FindAsync(new object[] { id }, cancellationToken);
            if (entry != null)
            {
                entry.IsFavorite = isFavorite;
                await Context.SaveChangesAsync(cancellationToken);

                stopwatch.Stop();

                _logger.LogInformation(
                    "Set favorite status for TTS history entry {Id} to {IsFavorite} in {ElapsedMs}ms",
                    id, isFavorite, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                stopwatch.Stop();

                _logger.LogWarning(
                    "TTS history entry {Id} not found when setting favorite status in {ElapsedMs}ms",
                    id, stopwatch.ElapsedMilliseconds);
            }

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "TtsMessageHistoryRepository.SetFavoriteAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, Id={Id}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, id);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to set favorite status for TTS history entry {Id}. ElapsedMs={ElapsedMs}, Error={Error}",
                id, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }
}
