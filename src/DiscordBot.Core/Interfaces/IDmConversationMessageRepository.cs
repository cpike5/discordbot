using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

public interface IDmConversationMessageRepository : IRepository<DmConversationMessage>
{
    Task<IEnumerable<DmConversationMessage>> GetRecentByUserAsync(
        ulong userId, int limit, CancellationToken ct = default);

    Task DeleteOldestByUserAsync(
        ulong userId, int keepCount, CancellationToken ct = default);
}
