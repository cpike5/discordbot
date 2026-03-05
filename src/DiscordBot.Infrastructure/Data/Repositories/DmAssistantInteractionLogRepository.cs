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
}
