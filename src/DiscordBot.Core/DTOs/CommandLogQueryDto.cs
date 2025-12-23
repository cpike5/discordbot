namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for querying command logs with filters and pagination.
/// </summary>
public class CommandLogQueryDto
{
    /// <summary>
    /// Gets or sets the search term for multi-field search across command name, username, and guild name. Null or empty means no search filter.
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Gets or sets the guild ID filter. Null means no filter.
    /// </summary>
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Gets or sets the user ID filter. Null means no filter.
    /// </summary>
    public ulong? UserId { get; set; }

    /// <summary>
    /// Gets or sets the command name filter. Null means no filter.
    /// </summary>
    public string? CommandName { get; set; }

    /// <summary>
    /// Gets or sets the start date filter. Null means no start date restriction.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Gets or sets the end date filter. Null means no end date restriction.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Gets or sets whether to return only successful commands. Null means return all.
    /// </summary>
    public bool? SuccessOnly { get; set; }

    /// <summary>
    /// Gets or sets the page number (1-based).
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    public int PageSize { get; set; } = 50;
}
