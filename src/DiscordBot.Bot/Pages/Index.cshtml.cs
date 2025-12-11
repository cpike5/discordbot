using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Bot.ViewModels.Pages;

namespace DiscordBot.Bot.Pages;

[Authorize(Policy = "RequireViewer")]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IBotService _botService;
    private readonly IGuildService _guildService;
    private readonly ICommandLogService _commandLogService;

    public BotStatusViewModel BotStatus { get; private set; } = default!;
    public GuildStatsViewModel GuildStats { get; private set; } = default!;
    public CommandStatsViewModel CommandStats { get; private set; } = default!;
    public RecentActivityViewModel RecentActivity { get; private set; } = default!;

    public IndexModel(
        ILogger<IndexModel> logger,
        IBotService botService,
        IGuildService guildService,
        ICommandLogService commandLogService)
    {
        _logger = logger;
        _botService = botService;
        _guildService = guildService;
        _commandLogService = commandLogService;
    }

    public async Task OnGetAsync()
    {
        _logger.LogDebug("Index page accessed");

        var statusDto = _botService.GetStatus();
        BotStatus = BotStatusViewModel.FromDto(statusDto);

        _logger.LogTrace("Bot status retrieved: {ConnectionState}, Latency: {LatencyMs}ms, Guilds: {GuildCount}",
            BotStatus.ConnectionState, BotStatus.LatencyMs, BotStatus.GuildCount);

        var guilds = await _guildService.GetAllGuildsAsync();
        GuildStats = GuildStatsViewModel.FromGuilds(guilds);

        _logger.LogDebug("Guild stats retrieved: Total: {TotalGuilds}, Active: {ActiveGuilds}, Inactive: {InactiveGuilds}",
            GuildStats.TotalGuilds, GuildStats.ActiveGuilds, GuildStats.InactiveGuilds);

        // Get command statistics for last 24 hours by default
        var since = DateTime.UtcNow.AddHours(-24);
        var commandStats = await _commandLogService.GetCommandStatsAsync(since);
        CommandStats = CommandStatsViewModel.FromStats(commandStats, timeRangeHours: 24);

        _logger.LogDebug("Command stats retrieved: Total: {TotalCommands}, Top command: {TopCommand}",
            CommandStats.TotalCommands,
            CommandStats.TopCommands.FirstOrDefault()?.CommandName ?? "None");

        // Get recent activity (last 10 command logs)
        var recentLogsResponse = await _commandLogService.GetLogsAsync(new CommandLogQueryDto
        {
            Page = 1,
            PageSize = 10
        });
        RecentActivity = RecentActivityViewModel.FromLogs(recentLogsResponse.Items);

        _logger.LogDebug("Recent activity retrieved: {ActivityCount} items",
            RecentActivity.Activities.Count);
    }
}
