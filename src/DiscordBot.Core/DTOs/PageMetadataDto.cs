namespace DiscordBot.Core.DTOs;

public class PageMetadataDto
{
    public string Name { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? RequiredPolicy { get; set; }
    public IReadOnlyList<string> Keywords { get; set; } = [];
    public string? IconName { get; set; }
    public string? Section { get; set; }
}
