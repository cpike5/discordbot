using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for querying scheduled messages with filters and pagination.
/// </summary>
public class ScheduledMessageQueryDto
{
    /// <summary>
    /// Gets or sets the guild ID filter. Null means no filter.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the channel ID filter. Null means no filter.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the frequency filter. Null means no filter.
    /// </summary>
    public ScheduleFrequency? Frequency { get; set; }

    /// <summary>
    /// Gets or sets the enabled status filter. Null means no filter.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the search term for title and content search. Null or empty means no search filter.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Gets or sets the start date filter for NextExecutionAt. Null means no start date restriction.
    /// </summary>
    public DateTime? NextExecutionAfter { get; set; }

    /// <summary>
    /// Gets or sets the end date filter for NextExecutionAt. Null means no end date restriction.
    /// </summary>
    public DateTime? NextExecutionBefore { get; set; }

    /// <summary>
    /// Gets or sets the page number (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size (maximum 100).
    /// </summary>
    public int PageSize { get; set; } = 25;
}
