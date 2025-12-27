using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for querying audit logs with filtering and pagination.
/// All filter properties are optional to allow flexible queries.
/// </summary>
public class AuditLogQueryDto
{
    /// <summary>
    /// Gets or sets the category to filter by.
    /// </summary>
    public AuditLogCategory? Category { get; set; }

    /// <summary>
    /// Gets or sets the action to filter by.
    /// </summary>
    public AuditLogAction? Action { get; set; }

    /// <summary>
    /// Gets or sets the actor ID to filter by.
    /// </summary>
    public string? ActorId { get; set; }

    /// <summary>
    /// Gets or sets the actor type to filter by.
    /// </summary>
    public AuditLogActorType? ActorType { get; set; }

    /// <summary>
    /// Gets or sets the target type to filter by.
    /// </summary>
    public string? TargetType { get; set; }

    /// <summary>
    /// Gets or sets the target ID to filter by.
    /// </summary>
    public string? TargetId { get; set; }

    /// <summary>
    /// Gets or sets the guild ID to filter by.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the start of the date range to filter by.
    /// Filters for entries with Timestamp greater than or equal to StartDate.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end of the date range to filter by.
    /// Filters for entries with Timestamp less than or equal to EndDate.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the correlation ID to filter by.
    /// Used to retrieve all related log entries.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the search term for free-text search across Details field.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Gets or sets the page number for pagination (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size for pagination.
    /// </summary>
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets the field to sort by.
    /// Defaults to "Timestamp" for chronological ordering.
    /// </summary>
    public string SortBy { get; set; } = "Timestamp";

    /// <summary>
    /// Gets or sets whether to sort in descending order.
    /// Defaults to true (newest first).
    /// </summary>
    public bool SortDescending { get; set; } = true;
}
