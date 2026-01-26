using Discord.WebSocket;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Admin.MessageLogs;

/// <summary>
/// Page model for listing and searching message logs.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class IndexModel : PageModel
{
    private readonly IMessageLogService _messageLogService;
    private readonly IGuildService _guildService;
    private readonly IMessageLogRepository _messageLogRepository;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IMessageLogService messageLogService,
        IGuildService guildService,
        IMessageLogRepository messageLogRepository,
        DiscordSocketClient discordClient,
        ILogger<IndexModel> logger)
    {
        _messageLogService = messageLogService;
        _guildService = guildService;
        _messageLogRepository = messageLogRepository;
        _discordClient = discordClient;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public ulong? AuthorId { get; set; }

    [BindProperty(SupportsGet = true)]
    public ulong? GuildId { get; set; }

    [BindProperty(SupportsGet = true)]
    public ulong? ChannelId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Source { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    /// <summary>
    /// Display name for the selected author (populated from message logs).
    /// </summary>
    public string? AuthorUsername { get; set; }

    /// <summary>
    /// Display name for the selected guild.
    /// </summary>
    public string? GuildName { get; set; }

    /// <summary>
    /// Display name for the selected channel.
    /// </summary>
    public string? ChannelName { get; set; }

    public MessageLogListViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Handles GET requests - redirects to unified Logs page.
    /// </summary>
    public IActionResult OnGetAsync()
    {
        return RedirectToPage("/Admin/Logs", new { tab = "messages" });
    }
}
