using System.ComponentModel.DataAnnotations;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Criteria for bulk purge operations.
/// </summary>
public class BulkPurgeCriteriaDto
{
    /// <summary>
    /// The type of entity to purge.
    /// </summary>
    [Required]
    public BulkPurgeEntityType EntityType { get; set; }

    /// <summary>
    /// Start date for the purge range (inclusive). Null means no lower bound.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for the purge range (exclusive). Null means no upper bound.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Optional guild ID to limit purge to specific guild.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets a human-readable description of the date range.
    /// </summary>
    public string GetDateRangeDescription()
    {
        if (StartDate.HasValue && EndDate.HasValue)
        {
            return $"{StartDate.Value:yyyy-MM-dd} to {EndDate.Value:yyyy-MM-dd}";
        }

        if (StartDate.HasValue)
        {
            return $"from {StartDate.Value:yyyy-MM-dd}";
        }

        if (EndDate.HasValue)
        {
            return $"until {EndDate.Value:yyyy-MM-dd}";
        }

        return "all time";
    }
}
