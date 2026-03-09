using System.Diagnostics;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Tracing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for UserSoundFavorite entities with favorite-specific operations.
/// </summary>
public class UserSoundFavoriteRepository : Repository<UserSoundFavorite>, IUserSoundFavoriteRepository
{
    private readonly ILogger<UserSoundFavoriteRepository> _logger;
    private const int SlowOperationThresholdMs = 100;

    public UserSoundFavoriteRepository(
        BotDbContext context,
        ILogger<UserSoundFavoriteRepository> logger,
        ILogger<Repository<UserSoundFavorite>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<Guid>> GetFavoriteSoundIdsAsync(
        ulong userId,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetFavoriteSoundIdsAsync",
            entityType: nameof(UserSoundFavorite),
            dbOperation: "SELECT",
            entityId: $"{userId}:{guildId}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Retrieving favorite sound IDs for user {UserId} in guild {GuildId}",
            userId, guildId);

        try
        {
            var soundIds = await DbSet
                .AsNoTracking()
                .Where(f => f.UserId == userId && f.GuildId == guildId)
                .Select(f => f.SoundId)
                .ToListAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug(
                "Retrieved {Count} favorite sound IDs for user {UserId} in guild {GuildId} in {ElapsedMs}ms",
                soundIds.Count, userId, guildId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserSoundFavoriteRepository.GetFavoriteSoundIdsAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, UserId={UserId}, GuildId={GuildId}, Count={Count}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, userId, guildId, soundIds.Count);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return soundIds;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to retrieve favorite sound IDs for user {UserId} in guild {GuildId}. ElapsedMs={ElapsedMs}, Error={Error}",
                userId, guildId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task<UserSoundFavorite?> GetFavoriteAsync(
        ulong userId,
        Guid soundId,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetFavoriteAsync",
            entityType: nameof(UserSoundFavorite),
            dbOperation: "SELECT",
            entityId: $"{userId}:{soundId}:{guildId}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Retrieving favorite for user {UserId}, sound {SoundId} in guild {GuildId}",
            userId, soundId, guildId);

        try
        {
            var favorite = await DbSet
                .AsNoTracking()
                .Where(f => f.UserId == userId && f.SoundId == soundId && f.GuildId == guildId)
                .FirstOrDefaultAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug(
                "Retrieved favorite for user {UserId}, sound {SoundId} in guild {GuildId} in {ElapsedMs}ms. Found={Found}",
                userId, soundId, guildId, stopwatch.ElapsedMilliseconds, favorite != null);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserSoundFavoriteRepository.GetFavoriteAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, UserId={UserId}, SoundId={SoundId}, GuildId={GuildId}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, userId, soundId, guildId);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return favorite;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to retrieve favorite for user {UserId}, sound {SoundId} in guild {GuildId}. ElapsedMs={ElapsedMs}, Error={Error}",
                userId, soundId, guildId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task RemoveFavoriteAsync(
        ulong userId,
        Guid soundId,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "RemoveFavoriteAsync",
            entityType: nameof(UserSoundFavorite),
            dbOperation: "DELETE",
            entityId: $"{userId}:{soundId}:{guildId}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Removing favorite for user {UserId}, sound {SoundId} in guild {GuildId}",
            userId, soundId, guildId);

        try
        {
            var favorite = await DbSet
                .Where(f => f.UserId == userId && f.SoundId == soundId && f.GuildId == guildId)
                .FirstOrDefaultAsync(cancellationToken);

            if (favorite != null)
            {
                DbSet.Remove(favorite);
                await Context.SaveChangesAsync(cancellationToken);

                stopwatch.Stop();

                _logger.LogInformation(
                    "Removed favorite for user {UserId}, sound {SoundId} in guild {GuildId} in {ElapsedMs}ms",
                    userId, soundId, guildId, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                stopwatch.Stop();

                _logger.LogDebug(
                    "No favorite found to remove for user {UserId}, sound {SoundId} in guild {GuildId} in {ElapsedMs}ms",
                    userId, soundId, guildId, stopwatch.ElapsedMilliseconds);
            }

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserSoundFavoriteRepository.RemoveFavoriteAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, UserId={UserId}, SoundId={SoundId}, GuildId={GuildId}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, userId, soundId, guildId);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to remove favorite for user {UserId}, sound {SoundId} in guild {GuildId}. ElapsedMs={ElapsedMs}, Error={Error}",
                userId, soundId, guildId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }
}
