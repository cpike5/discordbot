using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Progress update for bulk purge operations sent via SignalR.
/// </summary>
public class BulkPurgeProgressDto
{
    /// <summary>
    /// The entity type being purged.
    /// </summary>
    public BulkPurgeEntityType EntityType { get; set; }

    /// <summary>
    /// Number of records processed so far.
    /// </summary>
    public int ProcessedCount { get; set; }

    /// <summary>
    /// Total number of records to process.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Percentage complete (0-100).
    /// </summary>
    public int PercentComplete => TotalCount > 0
        ? (int)Math.Round((double)ProcessedCount / TotalCount * 100)
        : 0;

    /// <summary>
    /// Whether the operation is complete.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Timestamp of this progress update.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional message about current operation.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Creates a progress update.
    /// </summary>
    public static BulkPurgeProgressDto Create(
        BulkPurgeEntityType entityType,
        int processedCount,
        int totalCount,
        bool isComplete = false,
        string? message = null)
    {
        return new BulkPurgeProgressDto
        {
            EntityType = entityType,
            ProcessedCount = processedCount,
            TotalCount = totalCount,
            IsComplete = isComplete,
            Message = message,
            Timestamp = DateTime.UtcNow
        };
    }
}
