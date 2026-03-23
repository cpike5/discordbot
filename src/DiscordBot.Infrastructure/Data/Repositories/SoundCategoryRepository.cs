using System.Diagnostics;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Tracing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for SoundCategory entities with category-specific operations.
/// </summary>
public class SoundCategoryRepository : Repository<SoundCategory>, ISoundCategoryRepository
{
    private readonly ILogger<SoundCategoryRepository> _logger;
    private const int SlowOperationThresholdMs = 100;

    public SoundCategoryRepository(
        BotDbContext context,
        ILogger<SoundCategoryRepository> logger,
        ILogger<Repository<SoundCategory>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<SoundCategory>> GetByGuildAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "GetByGuildAsync",
            entityType: nameof(SoundCategory),
            dbOperation: "SELECT",
            entityId: guildId.ToString());

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Retrieving sound categories for guild {GuildId}",
            guildId);

        try
        {
            var categories = await DbSet
                .AsNoTracking()
                .Where(c => c.GuildId == guildId)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Name)
                .ToListAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogDebug(
                "Retrieved {Count} sound categories for guild {GuildId} in {ElapsedMs}ms",
                categories.Count, guildId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "SoundCategoryRepository.GetByGuildAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, GuildId={GuildId}, Count={Count}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, guildId, categories.Count);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);

            return categories;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to retrieve sound categories for guild {GuildId}. ElapsedMs={ElapsedMs}, Error={Error}",
                guildId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }

    public async Task ReorderAsync(
        ulong guildId,
        IEnumerable<(int Id, int SortOrder)> ordering,
        CancellationToken cancellationToken = default)
    {
        using var activity = InfrastructureActivitySource.StartRepositoryActivity(
            operationName: "ReorderAsync",
            entityType: nameof(SoundCategory),
            dbOperation: "UPDATE",
            entityId: guildId.ToString());

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug(
            "Reordering sound categories for guild {GuildId}",
            guildId);

        try
        {
            var orderingList = ordering.ToList();
            var categoryIds = orderingList.Select(o => o.Id).ToList();

            var categories = await DbSet
                .Where(c => c.GuildId == guildId && categoryIds.Contains(c.Id))
                .ToListAsync(cancellationToken);

            foreach (var category in categories)
            {
                var newOrder = orderingList.FirstOrDefault(o => o.Id == category.Id);
                category.SortOrder = newOrder.SortOrder;
            }

            await Context.SaveChangesAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Reordered {Count} sound categories for guild {GuildId} in {ElapsedMs}ms",
                categories.Count, guildId, stopwatch.ElapsedMilliseconds);

            if (stopwatch.ElapsedMilliseconds > SlowOperationThresholdMs)
            {
                _logger.LogWarning(
                    "SoundCategoryRepository.ReorderAsync slow operation. ElapsedMs={ElapsedMs}, Threshold={ThresholdMs}ms, GuildId={GuildId}, Count={Count}",
                    stopwatch.ElapsedMilliseconds, SlowOperationThresholdMs, guildId, categories.Count);
            }

            InfrastructureActivitySource.CompleteActivity(activity, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            InfrastructureActivitySource.RecordException(activity, ex, stopwatch.ElapsedMilliseconds);

            _logger.LogError(ex,
                "Failed to reorder sound categories for guild {GuildId}. ElapsedMs={ElapsedMs}, Error={Error}",
                guildId, stopwatch.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }
}
