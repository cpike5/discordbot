using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for FlaggedEvent entities with auto-moderation-specific operations.
/// </summary>
public interface IFlaggedEventRepository : IRepository<FlaggedEvent>
{
    /// <summary>
    /// Gets pending flagged events for a guild with optional filtering and pagination.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="ruleType">Optional rule type filter.</param>
    /// <param name="severity">Optional severity filter.</param>
    /// <param name="userId">Optional user ID filter.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated events and the total count.</returns>
    Task<(IEnumerable<FlaggedEvent> Items, int TotalCount)> GetPendingEventsAsync(
        ulong guildId,
        RuleType? ruleType = null,
        Severity? severity = null,
        ulong? userId = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets flagged events for a specific user with pagination.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="userId">Discord user ID to filter by.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated events and the total count.</returns>
    Task<(IEnumerable<FlaggedEvent> Items, int TotalCount)> GetByUserAsync(
        ulong guildId,
        ulong userId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of flagged events grouped by status for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping status to count.</returns>
    Task<IDictionary<FlaggedEventStatus, int>> GetCountByStatusAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets flagged events for a guild with advanced filtering and pagination.
    /// </summary>
    /// <param name="guildId">Discord guild ID to filter by.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="ruleType">Optional rule type filter.</param>
    /// <param name="severity">Optional severity filter.</param>
    /// <param name="userId">Optional user ID filter.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the paginated events and the total count.</returns>
    Task<(IEnumerable<FlaggedEvent> Items, int TotalCount)> GetFilteredByGuildAsync(
        ulong guildId,
        FlaggedEventStatus? status = null,
        RuleType? ruleType = null,
        Severity? severity = null,
        ulong? userId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
