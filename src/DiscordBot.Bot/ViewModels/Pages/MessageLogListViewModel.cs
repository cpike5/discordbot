using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying a paginated list of message logs with search and filter capabilities.
/// </summary>
public class MessageLogListViewModel
{
    public IReadOnlyList<MessageLogDto> Messages { get; set; } = Array.Empty<MessageLogDto>();
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    // Filter values
    public ulong? AuthorId { get; set; }
    public ulong? GuildId { get; set; }
    public ulong? ChannelId { get; set; }
    public string? Source { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SearchTerm { get; set; }
}
