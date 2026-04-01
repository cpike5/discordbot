using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Models.FeatureRequests;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Business-logic contract for managing feature requests.
/// </summary>
public interface IFeatureRequestService
{
    Task<FeatureRequest> SubmitAsync(FeatureRequestSubmission submission);
    Task<FeatureRequest?> GetByIdAsync(Guid id);
    Task<(IEnumerable<FeatureRequest> Items, int Total)> GetByGuildIdAsync(
        ulong guildId, FeatureRequestStatus? statusFilter, int page, int pageSize);
    Task UpdateStatusAsync(Guid id, FeatureRequestStatus status, ulong? reviewerUserId, string? notes);
    Task SetDocGenResultAsync(Guid id, string? docPath, string? branchName, string? error);
}
