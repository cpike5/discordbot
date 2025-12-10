using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying guild statistics on the dashboard.
/// </summary>
public record GuildStatsViewModel
{
    /// <summary>
    /// Gets the total number of guilds the bot is in.
    /// </summary>
    public int TotalGuilds { get; init; }

    /// <summary>
    /// Gets the number of active guilds.
    /// </summary>
    public int ActiveGuilds { get; init; }

    /// <summary>
    /// Gets the number of inactive guilds.
    /// </summary>
    public int InactiveGuilds { get; init; }

    /// <summary>
    /// Creates a <see cref="GuildStatsViewModel"/> from a collection of guilds.
    /// </summary>
    /// <param name="guilds">The collection of guild DTOs to compute statistics from.</param>
    /// <returns>A new <see cref="GuildStatsViewModel"/> instance with computed statistics.</returns>
    public static GuildStatsViewModel FromGuilds(IEnumerable<GuildDto> guilds)
    {
        var guildList = guilds.ToList();
        var activeCount = guildList.Count(g => g.IsActive);
        var totalCount = guildList.Count;

        return new GuildStatsViewModel
        {
            TotalGuilds = totalCount,
            ActiveGuilds = activeCount,
            InactiveGuilds = totalCount - activeCount
        };
    }
}
