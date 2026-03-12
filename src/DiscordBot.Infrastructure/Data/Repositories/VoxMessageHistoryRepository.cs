using System.Diagnostics;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Tracing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for VoxMessageHistory entities with history-specific operations.
/// </summary>
public class VoxMessageHistoryRepository : Repository<VoxMessageHistory>, IVoxMessageHistoryRepository
{
    private readonly ILogger<VoxMessageHistoryRepository> _logger;
    private const int SlowOperationThresholdMs = 100;

    public VoxMessageHistoryRepository(
        BotDbContext context,
        ILogger<VoxMessageHistoryRepository> logger,
        ILogger<Repository<VoxMessageHistory>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<VoxMessageHistory>> GetRecentAsync(
        ulong userId,
        ulong guildId,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetRecentAsync",
            entityType: nameof(VoxMessageHistory),
            dbOperation: "SELECT",
            entityId: $"{userId}:{guildId}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Retrieving recent VOX history for user {UserId} in guild {GuildId}, limit {Limit}",
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
                "Retrieved {Count} recent VOX history entries for user {UserId} in guild {GuildId} in {ElapsedMs}ms",
                entries.Count, userId, guildId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "VoxMessageHistoryRepository.GetRecentAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, UserId={UserId}, GuildId={GuildId}, Count={Count}",
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
                "Failed to retrieve recent VOX history for user {UserId} in guild {GuildId}. ElapsedMs={ElapsedMs}, Error={Error}",
                userId, guildId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task<IReadOnlyList<VoxMessageHistory>> GetFavoritesAsync(
        ulong userId,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetFavoritesAsync",
            entityType: nameof(VoxMessageHistory),
            dbOperation: "SELECT",
            entityId: $"{userId}:{guildId}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Retrieving favorite VOX messages for user {UserId} in guild {GuildId}",
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
                "Retrieved {Count} favorite VOX messages for user {UserId} in guild {GuildId} in {ElapsedMs}ms",
                entries.Count, userId, guildId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "VoxMessageHistoryRepository.GetFavoritesAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, UserId={UserId}, GuildId={GuildId}, Count={Count}",
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
                "Failed to retrieve favorite VOX messages for user {UserId} in guild {GuildId}. ElapsedMs={ElapsedMs}, Error={Error}",
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
            entityType: nameof(VoxMessageHistory),
            dbOperation: "UPDATE",
            entityId: id.ToString());

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Setting favorite status for VOX history entry {Id} to {IsFavorite}",
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
                    "Set favorite status for VOX history entry {Id} to {IsFavorite} in {ElapsedMs}ms",
                    id, isFavorite, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                stopwatch.Stop();

                _logger.LogWarning(
                    "VOX history entry {Id} not found when setting favorite status in {ElapsedMs}ms",
                    id, stopwatch.ElapsedMilliseconds);
            }

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "VoxMessageHistoryRepository.SetFavoriteAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, Id={Id}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, id);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to set favorite status for VOX history entry {Id}. ElapsedMs={ElapsedMs}, Error={Error}",
                id, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }
}
