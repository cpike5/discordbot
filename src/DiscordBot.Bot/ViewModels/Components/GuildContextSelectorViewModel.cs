namespace DiscordBot.Bot.ViewModels.Components;

public record GuildContextSelectorViewModel
{
    public string RouteTemplate { get; init; } = string.Empty;
    public IReadOnlyList<GuildSelectorItem> Guilds { get; init; } = [];
}

public record GuildSelectorItem
{
    public string GuildId { get; init; } = string.Empty;
    public string GuildName { get; init; } = string.Empty;
    public string? GuildIconUrl { get; init; }
}
