using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

public class DmConversationMessageRepository : Repository<DmConversationMessage>, IDmConversationMessageRepository
{
    private readonly ILogger<DmConversationMessageRepository> _logger;

    public DmConversationMessageRepository(
        BotDbContext context,
        ILogger<DmConversationMessageRepository> logger,
        ILogger<Repository<DmConversationMessage>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    public async Task<IEnumerable<DmConversationMessage>> GetRecentByUserAsync(
        ulong userId, int limit, CancellationToken ct = default)
    {
        // Get most recent messages, then reverse to oldest-first for conversation order
        var messages = await DbSet
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Timestamp)
            .Take(limit)
            .ToListAsync(ct);

        messages.Reverse();
        return messages;
    }

    public async Task DeleteOldestByUserAsync(
        ulong userId, int keepCount, CancellationToken ct = default)
    {
        var totalCount = await DbSet
            .CountAsync(m => m.UserId == userId, ct);

        if (totalCount <= keepCount)
            return;

        var deleteCount = totalCount - keepCount;

        _logger.LogDebug(
            "Trimming {DeleteCount} oldest DM conversation messages for user {UserId}",
            deleteCount, userId);

        // Delete oldest messages beyond the keep count
        var oldestIds = await DbSet
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Timestamp)
            .Take(deleteCount)
            .Select(m => m.Id)
            .ToListAsync(ct);

        await DbSet
            .Where(m => oldestIds.Contains(m.Id))
            .ExecuteDeleteAsync(ct);
    }
}
