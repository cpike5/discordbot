using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

public interface IPageMetadataService
{
    IReadOnlyList<PageMetadataDto> GetAllPages();
    IReadOnlyList<PageMetadataDto> SearchPages(string searchTerm);
    PageMetadataDto? FindExactMatch(string searchTerm);
}
