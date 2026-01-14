using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Preview result for a bulk purge operation showing estimated counts.
/// </summary>
public class BulkPurgePreviewDto
{
    /// <summary>
    /// Whether the preview was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if preview failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The entity type being previewed.
    /// </summary>
    public BulkPurgeEntityType EntityType { get; set; }

    /// <summary>
    /// Estimated number of records that would be deleted.
    /// </summary>
    public int EstimatedCount { get; set; }

    /// <summary>
    /// Human-readable description of the date range.
    /// </summary>
    public string DateRange { get; set; } = string.Empty;

    /// <summary>
    /// Guild ID if filtering by guild.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Creates a successful preview result.
    /// </summary>
    public static BulkPurgePreviewDto Succeeded(
        BulkPurgeEntityType entityType,
        int estimatedCount,
        string dateRange,
        ulong? guildId = null)
    {
        return new BulkPurgePreviewDto
        {
            Success = true,
            EntityType = entityType,
            EstimatedCount = estimatedCount,
            DateRange = dateRange,
            GuildId = guildId
        };
    }

    /// <summary>
    /// Creates a failed preview result.
    /// </summary>
    public static BulkPurgePreviewDto Failed(string errorMessage)
    {
        return new BulkPurgePreviewDto
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
