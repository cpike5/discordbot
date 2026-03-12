using System.Diagnostics;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Tracing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for UserTtsPreset entities with preset-specific operations.
/// </summary>
public class UserTtsPresetRepository : Repository<UserTtsPreset>, IUserTtsPresetRepository
{
    private readonly ILogger<UserTtsPresetRepository> _logger;
    private const int SlowOperationThresholdMs = 100;

    public UserTtsPresetRepository(
        BotDbContext context,
        ILogger<UserTtsPresetRepository> logger,
        ILogger<Repository<UserTtsPreset>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<UserTtsPreset>> GetByUserIdAsync(
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetByUserIdAsync",
            entityType: nameof(UserTtsPreset),
            dbOperation: "SELECT",
            entityId: userId.ToString());

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug("Retrieving TTS presets for user {UserId}", userId);

        try
        {
            var presets = await DbSet
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .OrderBy(p => p.CreatedAt)
                .ToListAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug(
                "Retrieved {Count} TTS presets for user {UserId} in {ElapsedMs}ms",
                presets.Count, userId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserTtsPresetRepository.GetByUserIdAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, UserId={UserId}, Count={Count}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, userId, presets.Count);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return presets;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to retrieve TTS presets for user {UserId}. ElapsedMs={ElapsedMs}, Error={Error}",
                userId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public new async Task<UserTtsPreset?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetByIdAsync",
            entityType: nameof(UserTtsPreset),
            dbOperation: "SELECT",
            entityId: id.ToString());

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug("Retrieving TTS preset {PresetId}", id);

        try
        {
            var preset = await DbSet
                .AsNoTracking()
                .Where(p => p.Id == id)
                .FirstOrDefaultAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug(
                "Retrieved TTS preset {PresetId} in {ElapsedMs}ms. Found={Found}",
                id, stopwatch.ElapsedMilliseconds, preset != null);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserTtsPresetRepository.GetByIdAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, PresetId={PresetId}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, id);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return preset;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to retrieve TTS preset {PresetId}. ElapsedMs={ElapsedMs}, Error={Error}",
                id, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task<int> GetCountByUserIdAsync(
        ulong userId,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetCountByUserIdAsync",
            entityType: nameof(UserTtsPreset),
            dbOperation: "COUNT",
            entityId: userId.ToString());

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug("Counting TTS presets for user {UserId}", userId);

        try
        {
            var count = await DbSet
                .Where(p => p.UserId == userId)
                .CountAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug(
                "Counted {Count} TTS presets for user {UserId} in {ElapsedMs}ms",
                count, userId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserTtsPresetRepository.GetCountByUserIdAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, UserId={UserId}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, userId);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return count;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to count TTS presets for user {UserId}. ElapsedMs={ElapsedMs}, Error={Error}",
                userId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }
}
