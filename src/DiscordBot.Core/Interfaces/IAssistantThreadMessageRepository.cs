using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>Persistence for <see cref="AssistantThreadMessage"/> rows.</summary>
public interface IAssistantThreadMessageRepository : IRepository<AssistantThreadMessage>
{
    /// <summary>The most recent <paramref name="limit"/> turns of one thread, oldest first.</summary>
    Task<IReadOnlyList<AssistantThreadMessage>> GetRecentByThreadAsync(ulong threadId, int limit, CancellationToken ct = default);

    /// <summary>Deletes every turn of a thread but the most recent <paramref name="keepCount"/>.</summary>
    Task DeleteOldestByThreadAsync(ulong threadId, int keepCount, CancellationToken ct = default);

    /// <summary>Deletes every turn attributed to a user, in any thread.</summary>
    /// <returns>How many rows were deleted.</returns>
    Task<int> DeleteByUserAsync(ulong userId, CancellationToken ct = default);
}
