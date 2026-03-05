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
        // Order by Id (auto-increment) to guarantee deterministic ordering
        // even when user+assistant messages share the same Timestamp
        var messages = await DbSet
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync(ct);

        messages.Reverse();
        return messages;
    }

    public async Task DeleteOldestByUserAsync(
        ulong userId, int keepCount, CancellationToken ct = default)
    {
        // Single subquery delete — no separate COUNT needed, atomic
        var keepIds = DbSet
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Id)
            .Take(keepCount)
            .Select(m => m.Id);

        var deleted = await DbSet
            .Where(m => m.UserId == userId && !keepIds.Contains(m.Id))
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogDebug(
                "Trimmed {DeleteCount} oldest DM conversation messages for user {UserId}",
                deleted, userId);
        }
    }
}
