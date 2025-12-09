using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Models;

/// <summary>
/// State data for guilds pagination interactions.
/// </summary>
public class GuildsPaginationState
{
    /// <summary>
    /// The list of all guilds to paginate through.
    /// </summary>
    public required List<GuildDto> Guilds { get; set; }

    /// <summary>
    /// The current page number (0-based).
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    /// The number of items per page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// The total number of pages.
    /// </summary>
    public int TotalPages { get; set; }
}
