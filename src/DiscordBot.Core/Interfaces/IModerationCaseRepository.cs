using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for ModerationCase entities with moderation-specific operations.
/// </summary>
public interface IModerationCaseRepository : IRepository<ModerationCase>
{
    /// <summary>
    /// Gets moderation cases for a guild with optional filtering and pagination.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="type">Optional case type filter.</param>
    /// <param name="targetUserId">Optional target user ID filter.</param>
    /// <param name="moderatorUserId">Optional moderator user ID filter.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated cases and the total count.</returns>
    Task<(IEnumerable<ModerationCase> Items, int TotalCount)> GetByGuildAsync(
        ulong guildId,
        CaseType? type = null,
        ulong? targetUserId = null,
        ulong? moderatorUserId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets moderation cases for a specific user with pagination.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="userId">Discord user ID to filter by.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated cases and the total count.</returns>
    Task<(IEnumerable<ModerationCase> Items, int TotalCount)> GetByUserAsync(
        ulong guildId,
        ulong userId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the next case number for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The next sequential case number for the guild.</returns>
    Task<long> GetNextCaseNumberAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a moderation case by guild and case number.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="caseNumber">Case number within the guild.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The moderation case, or null if not found.</returns>
    Task<ModerationCase?> GetByCaseNumberAsync(
        ulong guildId,
        long caseNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets expired temporary bans/mutes that need to be lifted.
    /// </summary>
    /// <param name="beforeTime">UTC time to compare against ExpiresAt.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of cases that have expired.</returns>
    Task<IEnumerable<ModerationCase>> GetExpiredCasesAsync(
        DateTime beforeTime,
        CancellationToken cancellationToken = default);
}
