using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

public class DmAssistantInteractionLogRepository : Repository<DmAssistantInteractionLog>, IDmAssistantInteractionLogRepository
{
    private readonly ILogger<DmAssistantInteractionLogRepository> _logger;

    public DmAssistantInteractionLogRepository(
        BotDbContext context,
        ILogger<DmAssistantInteractionLogRepository> logger,
        ILogger<Repository<DmAssistantInteractionLog>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<DmAssistantInteractionLog>> GetRecentByUserAsync(
        ulong userId, int limit, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(l => l.User)
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Deleting DM assistant interaction logs older than {CutoffDate}", cutoffDate);

        var deletedCount = await DbSet
            .Where(l => l.Timestamp < cutoffDate)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation(
            "Deleted {Count} DM assistant interaction logs older than {CutoffDate}",
            deletedCount, cutoffDate);

        return deletedCount;
    }

    /// <inheritdoc />
    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate, int batchSize, CancellationToken ct = default)
    {
        // Clamped to 1000 - see LlmUsageRepository.DeleteOlderThanAsync / AssistantInteractionLogRepository.
        batchSize = Math.Clamp(batchSize, 1, 1000);

        var idsToDelete = await DbSet
            .Where(l => l.Timestamp < cutoffDate)
            .OrderBy(l => l.Id)
            .Select(l => l.Id)
            .Take(batchSize)
            .ToListAsync(ct);

        if (idsToDelete.Count == 0)
        {
            return 0;
        }

        var deletedCount = await DbSet
            .Where(l => idsToDelete.Contains(l.Id))
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation(
            "Deleted {Count} DM assistant interaction logs (batch) older than {CutoffDate}",
            deletedCount, cutoffDate);

        return deletedCount;
    }
}
