using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Core.Interfaces;
using DiscordBot.Bot.ViewModels.Pages;

namespace DiscordBot.Bot.Pages;

[Authorize(Policy = "RequireViewer")]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IBotService _botService;
    private readonly IGuildService _guildService;

    public BotStatusViewModel BotStatus { get; private set; } = default!;
    public GuildStatsViewModel GuildStats { get; private set; } = default!;

    public IndexModel(ILogger<IndexModel> logger, IBotService botService, IGuildService guildService)
    {
        _logger = logger;
        _botService = botService;
        _guildService = guildService;
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
    }
}
