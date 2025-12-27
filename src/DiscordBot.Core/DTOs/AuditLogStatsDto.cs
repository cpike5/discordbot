using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object containing statistical information about audit logs.
/// Used for dashboard displays and reporting.
/// </summary>
public class AuditLogStatsDto
{
    /// <summary>
    /// Gets or sets the total number of audit log entries.
    /// </summary>
    public long TotalEntries { get; set; }

    /// <summary>
    /// Gets or sets the number of entries in the last 24 hours.
    /// </summary>
    public int Last24Hours { get; set; }

    /// <summary>
    /// Gets or sets the number of entries in the last 7 days.
    /// </summary>
    public int Last7Days { get; set; }

    /// <summary>
    /// Gets or sets the number of entries in the last 30 days.
    /// </summary>
    public int Last30Days { get; set; }

    /// <summary>
    /// Gets or sets the breakdown of entries by category.
    /// Key is the category, value is the count.
    /// </summary>
    public Dictionary<AuditLogCategory, int> ByCategory { get; set; } = new();

    /// <summary>
    /// Gets or sets the breakdown of entries by action.
    /// Key is the action, value is the count.
    /// </summary>
    public Dictionary<AuditLogAction, int> ByAction { get; set; } = new();

    /// <summary>
    /// Gets or sets the breakdown of entries by actor type.
    /// Key is the actor type, value is the count.
    /// </summary>
    public Dictionary<AuditLogActorType, int> ByActorType { get; set; } = new();

    /// <summary>
    /// Gets or sets the most active actors (top 10).
    /// Key is the actor ID, value is the count of actions.
    /// </summary>
    public Dictionary<string, int> TopActors { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp of the oldest entry.
    /// </summary>
    public DateTime? OldestEntry { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the newest entry.
    /// </summary>
    public DateTime? NewestEntry { get; set; }
}
