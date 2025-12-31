using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for managing flagged events from auto-moderation detection.
/// </summary>
public interface IFlaggedEventService
{
    /// <summary>
    /// Creates a new flagged event.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="channelId">The channel ID where the event occurred (optional).</param>
    /// <param name="ruleType">The rule type that triggered the event.</param>
    /// <param name="severity">The severity level.</param>
    /// <param name="description">The description of what was detected.</param>
    /// <param name="evidence">The evidence in JSON format.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created flagged event DTO.</returns>
    Task<FlaggedEventDto> CreateEventAsync(ulong guildId, ulong userId, ulong? channelId, RuleType ruleType, Severity severity, string description, string evidence, CancellationToken ct = default);

    /// <summary>
    /// Gets a flagged event by ID.
    /// </summary>
    /// <param name="eventId">The event ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The flagged event DTO, or null if not found.</returns>
    Task<FlaggedEventDto?> GetEventAsync(Guid eventId, CancellationToken ct = default);

    /// <summary>
    /// Gets all pending flagged events for a guild with pagination.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the pending events and total count.</returns>
    Task<(IEnumerable<FlaggedEventDto> Items, int TotalCount)> GetPendingEventsAsync(ulong guildId, int page = 1, int pageSize = 20, CancellationToken ct = default);

    /// <summary>
    /// Gets filtered flagged events for a guild with advanced filtering and pagination.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="query">The query parameters containing filters and pagination settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the filtered events and total count.</returns>
    Task<(IEnumerable<FlaggedEventDto> Items, int TotalCount)> GetFilteredEventsAsync(ulong guildId, FlaggedEventQueryDto query, CancellationToken ct = default);

    /// <summary>
    /// Dismisses a flagged event (marks as not requiring action).
    /// </summary>
    /// <param name="eventId">The event ID.</param>
    /// <param name="reviewerId">The moderator dismissing the event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated flagged event DTO, or null if not found.</returns>
    Task<FlaggedEventDto?> DismissEventAsync(Guid eventId, ulong reviewerId, CancellationToken ct = default);

    /// <summary>
    /// Acknowledges a flagged event (marks as seen but not yet actioned).
    /// </summary>
    /// <param name="eventId">The event ID.</param>
    /// <param name="reviewerId">The moderator acknowledging the event.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated flagged event DTO, or null if not found.</returns>
    Task<FlaggedEventDto?> AcknowledgeEventAsync(Guid eventId, ulong reviewerId, CancellationToken ct = default);

    /// <summary>
    /// Takes action on a flagged event (marks as actioned and records the action taken).
    /// </summary>
    /// <param name="eventId">The event ID.</param>
    /// <param name="action">The action taken.</param>
    /// <param name="reviewerId">The moderator taking action.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated flagged event DTO, or null if not found.</returns>
    Task<FlaggedEventDto?> TakeActionAsync(Guid eventId, string action, ulong reviewerId, CancellationToken ct = default);

    /// <summary>
    /// Gets all flagged events for a specific user in a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A collection of flagged events for the user.</returns>
    Task<IEnumerable<FlaggedEventDto>> GetUserEventsAsync(ulong guildId, ulong userId, CancellationToken ct = default);
}
