using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Dashboard;

/// <summary>
/// Default <see cref="IDashboardStatsProvider"/>. Scoped, because it reads through the request's
/// DbContext.
/// </summary>
public class DashboardStatsProvider : IDashboardStatsProvider
{
    /// <summary>The rolling window the command count and uptime percentage cover.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);

    private readonly IGuildService _guildService;
    private readonly ICommandLogService _commandLogService;
    private readonly IConnectionStateService _connectionStateService;

    public DashboardStatsProvider(
        IGuildService guildService,
        ICommandLogService commandLogService,
        IConnectionStateService connectionStateService)
    {
        _guildService = guildService;
        _commandLogService = commandLogService;
        _connectionStateService = connectionStateService;
    }

    /// <inheritdoc />
    public async Task<DashboardStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        // Sequential: both read through the same scoped DbContext.
        var guilds = await _guildService.GetAllGuildsAsync(cancellationToken);
        var commandCounts = await _commandLogService.GetCommandStatsAsync(DateTime.UtcNow - Window, cancellationToken);

        return Build(
            guilds,
            commandCounts.Values.Sum(),
            _connectionStateService.GetUptimePercentage(Window));
    }

    /// <summary>
    /// Shapes the hero numbers from data a caller already holds, so the page does not query twice.
    /// Servers and members count only guilds the bot is still in.
    /// </summary>
    public static DashboardStatsDto Build(IEnumerable<GuildDto> guilds, int commandsLast24Hours, double uptimePercent24Hours)
    {
        var active = guilds.Where(g => g.IsActive).ToList();
        return new DashboardStatsDto
        {
            TotalServers = active.Count,
            TotalMembers = active.Sum(g => g.MemberCount ?? 0),
            CommandsLast24Hours = commandsLast24Hours,
            UptimePercent24Hours = Math.Round(uptimePercent24Hours, 1),
            Timestamp = DateTime.UtcNow
        };
    }
}
