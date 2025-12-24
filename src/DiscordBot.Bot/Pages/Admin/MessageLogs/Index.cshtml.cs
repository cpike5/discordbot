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
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IMessageLogService messageLogService,
        ILogger<IndexModel> logger)
    {
        _messageLogService = messageLogService;
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

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    public MessageLogListViewModel ViewModel { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
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
}
