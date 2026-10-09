using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>Persistence for <see cref="AssistantThread"/> rows.</summary>
public interface IAssistantThreadRepository : IRepository<AssistantThread>
{
    /// <summary>The thread row, or null when the thread is not one the assistant holds.</summary>
    Task<AssistantThread?> GetByThreadIdAsync(ulong threadId, CancellationToken ct = default);

    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> threads whose last activity is before
    /// <paramref name="cutoff"/>; their messages go with them by cascade.
    /// </summary>
    /// <returns>How many threads were deleted.</returns>
    Task<int> DeleteInactiveOlderThanAsync(DateTime cutoff, int batchSize, CancellationToken ct = default);

    /// <summary>
    /// Sets <see cref="AssistantThread.StarterUserId"/> to 0 on every thread the user started.
    /// The thread belongs to the guild; only the pointer at the person goes.
    /// </summary>
    /// <returns>How many rows changed.</returns>
    Task<int> AnonymizeStarterAsync(ulong userId, CancellationToken ct = default);
}
