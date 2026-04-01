using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Data access contract for feature request entities.
/// </summary>
public interface IFeatureRequestRepository
{
    Task<FeatureRequest> AddAsync(FeatureRequest entity);
    Task<FeatureRequest?> GetByIdAsync(Guid id);
    Task<(IEnumerable<FeatureRequest> Items, int Total)> GetByGuildIdAsync(
        ulong guildId, FeatureRequestStatus? statusFilter, int page, int pageSize);
    Task UpdateAsync(FeatureRequest entity);
    Task AddRejectionAsync(FeatureRequestRejection rejection);
}
