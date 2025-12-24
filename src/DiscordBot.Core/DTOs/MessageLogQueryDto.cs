using DiscordBot.Core.Enums;

namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for querying message logs with filters and pagination.
/// </summary>
public class MessageLogQueryDto
{
    /// <summary>
    /// Gets or sets the author ID filter. Null means no filter.
    /// </summary>
    public ulong? AuthorId { get; set; }

    /// <summary>
    /// Gets or sets the guild ID filter. Null means no filter.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the channel ID filter. Null means no filter.
    /// </summary>
    public ulong? ChannelId { get; set; }

    /// <summary>
    /// Gets or sets the message source filter (DirectMessage or ServerChannel). Null means no filter.
    /// </summary>
    public MessageSource? Source { get; set; }

    /// <summary>
    /// Gets or sets the start date filter. Null means no start date restriction.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date filter. Null means no end date restriction.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets the search term for content search. Null or empty means no search filter.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Gets or sets the page number (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size (maximum 100).
    /// </summary>
    public int PageSize { get; set; } = 25;
}
