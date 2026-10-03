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

    /// <summary>
    /// Puts a request whose documentation run failed back in the queue: the status returns to
    /// <see cref="FeatureRequestStatus.Submitted"/> and the stored failure is cleared, so the next
    /// run of the documentation generator picks it up again. Nothing runs inline.
    /// </summary>
    /// <param name="id">The feature request ID.</param>
    /// <returns>True when the request was queued again; false when it does not exist or has not failed.</returns>
    Task<bool> RequeueDocGenAsync(Guid id);
}
