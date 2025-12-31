using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for managing moderation cases and moderation actions.
/// </summary>
public interface IModerationService
{
    /// <summary>
    /// Creates a new moderation case.
    /// </summary>
    /// <param name="dto">The moderation case creation data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created moderation case DTO.</returns>
    Task<ModerationCaseDto> CreateCaseAsync(ModerationCaseCreateDto dto, CancellationToken ct = default);

    /// <summary>
    /// Gets a moderation case by its ID.
    /// </summary>
    /// <param name="caseId">The case ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The moderation case DTO, or null if not found.</returns>
    Task<ModerationCaseDto?> GetCaseAsync(Guid caseId, CancellationToken ct = default);

    /// <summary>
    /// Gets a moderation case by its case number within a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="caseNumber">The case number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The moderation case DTO, or null if not found.</returns>
    Task<ModerationCaseDto?> GetCaseByNumberAsync(ulong guildId, long caseNumber, CancellationToken ct = default);

    /// <summary>
    /// Gets moderation cases matching the query criteria with pagination.
    /// </summary>
    /// <param name="query">The query criteria and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the matching cases and total count.</returns>
    Task<(IEnumerable<ModerationCaseDto> Items, int TotalCount)> GetCasesAsync(ModerationCaseQueryDto query, CancellationToken ct = default);

    /// <summary>
    /// Updates the reason for an existing moderation case.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="caseNumber">The case number.</param>
    /// <param name="reason">The new reason.</param>
    /// <param name="moderatorId">The moderator making the update.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated moderation case DTO, or null if not found.</returns>
    Task<ModerationCaseDto?> UpdateCaseReasonAsync(ulong guildId, long caseNumber, string reason, ulong moderatorId, CancellationToken ct = default);

    /// <summary>
    /// Gets all moderation cases for a specific user in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the user's cases and total count.</returns>
    Task<(IEnumerable<ModerationCaseDto> Items, int TotalCount)> GetUserCasesAsync(ulong guildId, ulong userId, int page = 1, int pageSize = 10, CancellationToken ct = default);

    /// <summary>
    /// Exports a user's moderation history to a file (PDF or CSV).
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The exported file as a byte array.</returns>
    Task<byte[]> ExportUserHistoryAsync(ulong guildId, ulong userId, CancellationToken ct = default);

    /// <summary>
    /// Gets moderation statistics for a moderator or the entire guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="moderatorId">The moderator ID (null for guild-wide stats).</param>
    /// <param name="startDate">The start date for the statistics period (null for all time).</param>
    /// <param name="endDate">The end date for the statistics period (null for current date).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The moderator statistics summary.</returns>
    Task<ModeratorStatsSummaryDto> GetModeratorStatsAsync(ulong guildId, ulong? moderatorId = null, DateTime? startDate = null, DateTime? endDate = null, CancellationToken ct = default);

    /// <summary>
    /// Gets all temporary moderation actions (temp bans, temp mutes) that have expired and need to be lifted.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of expired moderation cases.</returns>
    Task<IEnumerable<ModerationCase>> GetExpiredTemporaryActionsAsync(CancellationToken ct = default);
}
