using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Data.Repositories;

public class DmAssistantUsageMetricsRepository : Repository<DmAssistantUsageMetrics>, IDmAssistantUsageMetricsRepository
{
    public DmAssistantUsageMetricsRepository(
        BotDbContext context,
        ILogger<Repository<DmAssistantUsageMetrics>> baseLogger)
        : base(context, baseLogger)
    {
    }

    public async Task<DmAssistantUsageMetrics?> GetByUserAndDateAsync(
        ulong userId, DateTime date, CancellationToken ct = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Date == date.Date, ct);
    }
}
