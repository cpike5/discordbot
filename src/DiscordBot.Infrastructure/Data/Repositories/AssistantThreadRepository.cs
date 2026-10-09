using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

/// <summary>EF Core implementation of <see cref="IAssistantThreadRepository"/>.</summary>
public class AssistantThreadRepository : Repository<AssistantThread>, IAssistantThreadRepository
{
    private readonly ILogger<AssistantThreadRepository> _logger;

    public AssistantThreadRepository(
        BotDbContext context,
        ILogger<AssistantThreadRepository> logger,
        ILogger<Repository<AssistantThread>> baseLogger)
        : base(context, baseLogger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<AssistantThread?> GetByThreadIdAsync(ulong threadId, CancellationToken ct = default)
        => DbSet.FirstOrDefaultAsync(t => t.ThreadId == threadId, ct);

    /// <inheritdoc />
    public async Task<int> DeleteInactiveOlderThanAsync(DateTime cutoff, int batchSize, CancellationToken ct = default)
    {
        // Pick the batch first, then delete by key: the same id-select-then-ExecuteDelete shape the
        // interaction-log sweeps use, so a large backlog never holds one long transaction.
        var ids = await DbSet
            .Where(t => t.LastActivityAt < cutoff)
            .OrderBy(t => t.LastActivityAt)
            .Take(batchSize)
            .Select(t => t.ThreadId)
            .ToListAsync(ct);

        if (ids.Count == 0)
        {
            return 0;
        }

        var deleted = await DbSet
            .Where(t => ids.Contains(t.ThreadId))
            .ExecuteDeleteAsync(ct);

        _logger.LogDebug("Deleted {Count} assistant threads inactive since before {Cutoff}", deleted, cutoff);
        return deleted;
    }

    /// <inheritdoc />
    public Task<int> AnonymizeStarterAsync(ulong userId, CancellationToken ct = default)
        => DbSet
            .Where(t => t.StarterUserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.StarterUserId, 0UL), ct);
}
