using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for bulk data purge operations.
/// </summary>
public interface IBulkPurgeService
{
    /// <summary>
    /// Gets a preview of records that would be deleted without actually deleting.
    /// </summary>
    /// <param name="criteria">The purge criteria (entity type, date range, guild).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Preview with estimated record count.</returns>
    Task<BulkPurgePreviewDto> PreviewPurgeAsync(
        BulkPurgeCriteriaDto criteria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a bulk purge operation based on the specified criteria.
    /// </summary>
    /// <param name="criteria">The purge criteria (entity type, date range, guild).</param>
    /// <param name="adminUserId">The ID of the admin executing the purge.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with deleted record count.</returns>
    Task<BulkPurgeResultDto> ExecutePurgeAsync(
        BulkPurgeCriteriaDto criteria,
        string adminUserId,
        CancellationToken cancellationToken = default);
}
