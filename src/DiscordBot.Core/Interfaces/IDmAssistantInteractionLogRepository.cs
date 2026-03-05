using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

public interface IDmAssistantInteractionLogRepository : IRepository<DmAssistantInteractionLog>
{
    Task<IEnumerable<DmAssistantInteractionLog>> GetRecentByUserAsync(
        ulong userId, int limit, CancellationToken ct = default);

    Task<int> DeleteOlderThanAsync(
        DateTime cutoffDate, CancellationToken ct = default);
}
