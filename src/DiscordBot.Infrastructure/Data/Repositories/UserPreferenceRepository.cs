using System.Diagnostics;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Tracing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for UserPreference entities with preference-specific operations.
/// </summary>
public class UserPreferenceRepository : Repository<UserPreference>, IUserPreferenceRepository
{
    private readonly ILogger<UserPreferenceRepository> _logger;
    private const int SlowOperationThresholdMs = 100;

    public UserPreferenceRepository(
        BotDbContext context,
        ILogger<UserPreferenceRepository> logger,
        ILogger<Repository<UserPreference>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<UserPreference?> GetAsync(
        ulong userId,
        ulong guildId,
        string key,
        CancellationToken ct = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetAsync",
            entityType: nameof(UserPreference),
            dbOperation: "SELECT",
            entityId: $"{userId}:{guildId}:{key}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Retrieving preference for user {UserId} in guild {GuildId} with key {Key}",
            userId, guildId, key);

        try
        {
            var preference = await DbSet
                .AsNoTracking()
                .Where(p => p.UserId == userId && p.GuildId == guildId && p.Key == key)
                .FirstOrDefaultAsync(ct);

            stopwatch.Stop();

            _logger.LogDebug(
                "Retrieved preference for user {UserId} in guild {GuildId} with key {Key} in {ElapsedMs}ms. Found={Found}",
                userId, guildId, key, stopwatch.ElapsedMilliseconds, preference != null);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserPreferenceRepository.GetAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, UserId={UserId}, GuildId={GuildId}, Key={Key}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, userId, guildId, key);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return preference;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to retrieve preference for user {UserId} in guild {GuildId} with key {Key}. ElapsedMs={ElapsedMs}, Error={Error}",
                userId, guildId, key, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task<IReadOnlyList<UserPreference>> GetAllAsync(
        ulong userId,
        ulong guildId,
        CancellationToken ct = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetAllAsync",
            entityType: nameof(UserPreference),
            dbOperation: "SELECT",
            entityId: $"{userId}:{guildId}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Retrieving all preferences for user {UserId} in guild {GuildId}",
            userId, guildId);

        try
        {
            var preferences = await DbSet
                .AsNoTracking()
                .Where(p => p.UserId == userId && p.GuildId == guildId)
                .ToListAsync(ct);

            stopwatch.Stop();

            _logger.LogDebug(
                "Retrieved {Count} preferences for user {UserId} in guild {GuildId} in {ElapsedMs}ms",
                preferences.Count, userId, guildId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserPreferenceRepository.GetAllAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, UserId={UserId}, GuildId={GuildId}, Count={Count}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, userId, guildId, preferences.Count);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return preferences;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to retrieve preferences for user {UserId} in guild {GuildId}. ElapsedMs={ElapsedMs}, Error={Error}",
                userId, guildId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task SetAsync(
        ulong userId,
        ulong guildId,
        string key,
        string value,
        CancellationToken ct = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "SetAsync",
            entityType: nameof(UserPreference),
            dbOperation: "UPSERT",
            entityId: $"{userId}:{guildId}:{key}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Setting preference for user {UserId} in guild {GuildId} with key {Key}",
            userId, guildId, key);

        try
        {
            var existing = await DbSet
                .Where(p => p.UserId == userId && p.GuildId == guildId && p.Key == key)
                .FirstOrDefaultAsync(ct);

            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
                DbSet.Update(existing);
            }
            else
            {
                var preference = new UserPreference
                {
                    UserId = userId,
                    GuildId = guildId,
                    Key = key,
                    Value = value,
                    UpdatedAt = DateTime.UtcNow
                };
                await DbSet.AddAsync(preference, ct);
            }

            await Context.SaveChangesAsync(ct);

            stopwatch.Stop();

            _logger.LogInformation(
                "Set preference for user {UserId} in guild {GuildId} with key {Key} in {ElapsedMs}ms. IsUpdate={IsUpdate}",
                userId, guildId, key, stopwatch.ElapsedMilliseconds, existing != null);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserPreferenceRepository.SetAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, UserId={UserId}, GuildId={GuildId}, Key={Key}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, userId, guildId, key);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to set preference for user {UserId} in guild {GuildId} with key {Key}. ElapsedMs={ElapsedMs}, Error={Error}",
                userId, guildId, key, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task DeleteAsync(
        ulong userId,
        ulong guildId,
        string key,
        CancellationToken ct = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "DeleteAsync",
            entityType: nameof(UserPreference),
            dbOperation: "DELETE",
            entityId: $"{userId}:{guildId}:{key}");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Deleting preference for user {UserId} in guild {GuildId} with key {Key}",
            userId, guildId, key);

        try
        {
            var preference = await DbSet
                .Where(p => p.UserId == userId && p.GuildId == guildId && p.Key == key)
                .FirstOrDefaultAsync(ct);

            if (preference != null)
            {
                DbSet.Remove(preference);
                await Context.SaveChangesAsync(ct);

                stopwatch.Stop();

                _logger.LogInformation(
                    "Deleted preference for user {UserId} in guild {GuildId} with key {Key} in {ElapsedMs}ms",
                    userId, guildId, key, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                stopwatch.Stop();

                _logger.LogDebug(
                    "No preference found to delete for user {UserId} in guild {GuildId} with key {Key} in {ElapsedMs}ms",
                    userId, guildId, key, stopwatch.ElapsedMilliseconds);
            }

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "UserPreferenceRepository.DeleteAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, UserId={UserId}, GuildId={GuildId}, Key={Key}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, userId, guildId, key);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to delete preference for user {UserId} in guild {GuildId} with key {Key}. ElapsedMs={ElapsedMs}, Error={Error}",
                userId, guildId, key, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }
}
