using System.ComponentModel.DataAnnotations;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for querying notifications with filtering and pagination.
/// </summary>
public class NotificationQueryDto
{
    /// <summary>
    /// Filter by notification type. Null means no filter.
    /// </summary>
    public NotificationType? Type { get; set; }

    /// <summary>
    /// Filter by read status. Null = all, true = read only, false = unread only.
    /// </summary>
    public bool? IsRead { get; set; }

    /// <summary>
    /// Filter by severity (for PerformanceAlert types). Null means no filter.
    /// </summary>
    public AlertSeverity? Severity { get; set; }

    /// <summary>
    /// Start date for date range filter (UTC).
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for date range filter (UTC).
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Free-text search across Title and Message fields.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by guild ID. Null means all guilds.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Page number (1-based). Must be at least 1.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Page must be at least 1.")]
    public int Page { get; set; } = 1;

    /// <summary>
    /// Page size. Must be between 1 and 100.
    /// </summary>
    [Range(1, 100, ErrorMessage = "Page size must be between 1 and 100.")]
    public int PageSize { get; set; } = 25;
}
