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

    public async Task<IActionResult> OnGetAsync()
    {
        // Apply default date range (last 7 days) if no date filters specified
        if (!StartDate.HasValue && !EndDate.HasValue)
        {
            StartDate = DateTime.UtcNow.Date.AddDays(-7);
            EndDate = DateTime.UtcNow.Date.AddDays(1); // Include today's messages
        }

        _logger.LogDebug("Loading message logs with filters: AuthorId={AuthorId}, GuildId={GuildId}, ChannelId={ChannelId}, Source={Source}, StartDate={StartDate}, EndDate={EndDate}, SearchTerm={SearchTerm}, Page={Page}, PageSize={PageSize}",
            AuthorId, GuildId, ChannelId, Source, StartDate, EndDate, SearchTerm, CurrentPage, PageSize);

        // Parse source filter
        MessageSource? sourceFilter = null;
        if (!string.IsNullOrEmpty(Source))
        {
            if (Enum.TryParse<MessageSource>(Source, true, out var parsedSource))
            {
                sourceFilter = parsedSource;
            }
            else
            {
                _logger.LogWarning("Invalid source filter value: {Source}", Source);
            }
        }

        // Build query
        var query = new MessageLogQueryDto
        {
            AuthorId = AuthorId,
            GuildId = GuildId,
            ChannelId = ChannelId,
            Source = sourceFilter,
            StartDate = StartDate,
            EndDate = EndDate,
            SearchTerm = SearchTerm,
            Page = CurrentPage,
            PageSize = PageSize
        };

        // Get messages
        var result = await _messageLogService.GetLogsAsync(query);

        _logger.LogInformation("Retrieved {Count} message logs (page {Page} of {TotalPages})",
            result.Items.Count, result.Page, result.TotalPages);

        // Populate display names for autocomplete fields
        await PopulateDisplayNamesAsync();

        // Build view model
        ViewModel = new MessageLogListViewModel
        {
            Messages = result.Items,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            AuthorId = AuthorId,
            GuildId = GuildId,
            ChannelId = ChannelId,
            Source = Source,
            StartDate = StartDate,
            EndDate = EndDate,
            SearchTerm = SearchTerm
        };

        return Page();
    }

    /// <summary>
    /// Populates the display names for filter fields from their IDs.
    /// </summary>
    private async Task PopulateDisplayNamesAsync()
    {
        // Get author username from message logs if AuthorId is specified
        if (AuthorId.HasValue)
        {
            // Get the most recent message from this author to get their username
            var messages = await _messageLogRepository.GetUserMessagesAsync(
                AuthorId.Value,
                limit: 1);

            var message = messages.FirstOrDefault();
            AuthorUsername = message?.User?.Username;
        }

        // Get guild name if GuildId is specified
        if (GuildId.HasValue)
        {
            var guild = await _guildService.GetGuildByIdAsync(GuildId.Value);
            GuildName = guild?.Name;
        }

        // Get channel name if ChannelId is specified
        if (ChannelId.HasValue && GuildId.HasValue)
        {
            var socketGuild = _discordClient.GetGuild(GuildId.Value);
            var channel = socketGuild?.GetChannel(ChannelId.Value);
            ChannelName = channel?.Name;
        }
    }
}
