using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

public interface IDmAssistantUsageMetricsRepository : IRepository<DmAssistantUsageMetrics>
{
    Task<DmAssistantUsageMetrics?> GetByUserAndDateAsync(
        ulong userId, DateTime date, CancellationToken ct = default);
}
