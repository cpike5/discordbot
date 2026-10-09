using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>EF Core implementation of <see cref="IAssistantThreadMessageRepository"/>.</summary>
public class AssistantThreadMessageRepository : Repository<AssistantThreadMessage>, IAssistantThreadMessageRepository
{
    private readonly ILogger<AssistantThreadMessageRepository> _logger;

    public AssistantThreadMessageRepository(
        BotDbContext context,
        ILogger<AssistantThreadMessageRepository> logger,
        ILogger<Repository<AssistantThreadMessage>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AssistantThreadMessage>> GetRecentByThreadAsync(ulong threadId, int limit, CancellationToken ct = default)
    {
        // Ordered by Id, not Timestamp: a user turn and its reply are saved with one timestamp
        var messages = await DbSet
            .AsNoTracking()
            .Where(m => m.ThreadId == threadId)
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .ToListAsync(ct);

        messages.Reverse();
        return messages;
    }

    /// <inheritdoc />
    public async Task DeleteOldestByThreadAsync(ulong threadId, int keepCount, CancellationToken ct = default)
    {
        var keepIds = DbSet
            .Where(m => m.ThreadId == threadId)
            .OrderByDescending(m => m.Id)
            .Take(keepCount)
            .Select(m => m.Id);

        var deleted = await DbSet
            .Where(m => m.ThreadId == threadId && !keepIds.Contains(m.Id))
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            _logger.LogDebug("Trimmed {Count} oldest turns from assistant thread {ThreadId}", deleted, threadId);
        }
    }

    /// <inheritdoc />
    public Task<int> DeleteByUserAsync(ulong userId, CancellationToken ct = default)
        => DbSet.Where(m => m.UserId == userId).ExecuteDeleteAsync(ct);
}
