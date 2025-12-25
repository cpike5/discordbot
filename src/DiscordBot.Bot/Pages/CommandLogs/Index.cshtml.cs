using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;

namespace DiscordBot.Bot.Pages.CommandLogs;

/// <summary>
/// Page model for displaying command log list with filtering, pagination, and CSV export.
/// </summary>
[Authorize(Policy = "RequireModerator")]
public class IndexModel : PageModel
{
    private readonly ICommandLogService _commandLogService;
    private readonly IGuildService _guildService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ICommandLogService commandLogService,
        IGuildService guildService,
        ILogger<IndexModel> logger)
    {
        _commandLogService = commandLogService;
        _guildService = guildService;
        _logger = logger;
    }

    /// <summary>
    /// Search term for multi-field search across command name, username, and guild name.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by guild ID.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Filter by command name.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? CommandName { get; set; }

    /// <summary>
    /// Start date for date range filter.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for date range filter.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Filter by success status (null for all, true for success, false for failed).
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public bool? StatusFilter { get; set; }

    /// <summary>
    /// Current page number (1-based).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "page")]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Number of items per page.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    /// <summary>
    /// The view model containing command log list data.
    /// </summary>
    public CommandLogListViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Available guilds for the filter dropdown.
    /// </summary>
    public IReadOnlyList<GuildDto> AvailableGuilds { get; set; } = Array.Empty<GuildDto>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing command logs page. Search={Search}, Guild={Guild}, Command={Command}, Status={Status}, Page={Page}",
            SearchTerm, GuildId, CommandName, StatusFilter, CurrentPage);

        // Load guilds for dropdown
        AvailableGuilds = await _guildService.GetAllGuildsAsync(cancellationToken);

        // Set default date filter to "Today" if no filters are provided
        // This prevents showing all historical logs on first page load
        if (!StartDate.HasValue && !EndDate.HasValue && string.IsNullOrWhiteSpace(SearchTerm) &&
            !GuildId.HasValue && string.IsNullOrWhiteSpace(CommandName) && !StatusFilter.HasValue)
        {
            var today = DateTime.UtcNow.Date;
            StartDate = today;
            EndDate = today;
        }

        var query = new CommandLogQueryDto
        {
            SearchTerm = SearchTerm,
            GuildId = GuildId,
            CommandName = CommandName,
            StartDate = StartDate,
            EndDate = EndDate,
            SuccessOnly = StatusFilter,
            Page = CurrentPage,
            PageSize = PageSize
        };

        var paginatedLogs = await _commandLogService.GetLogsAsync(query, cancellationToken);

        var filters = new CommandLogFilterOptions
        {
            SearchTerm = SearchTerm,
            GuildId = GuildId,
            CommandName = CommandName,
            StartDate = StartDate,
            EndDate = EndDate,
            SuccessOnly = StatusFilter
        };

        ViewModel = CommandLogListViewModel.FromPaginatedDto(paginatedLogs, filters);

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("User exporting command logs. Filters: Search={Search}, Guild={GuildId}, Command={Command}, Status={Status}",
            SearchTerm, GuildId, CommandName, StatusFilter);

        var query = new CommandLogQueryDto
        {
            SearchTerm = SearchTerm,
            GuildId = GuildId,
            CommandName = CommandName,
            StartDate = StartDate,
            EndDate = EndDate,
            SuccessOnly = StatusFilter,
            Page = 1,
            PageSize = 10000 // Large limit for export
        };

        var result = await _commandLogService.GetLogsAsync(query, cancellationToken);

        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,Command,Guild,User,Duration (ms),Status,Error Message");

        foreach (var log in result.Items)
        {
            var timestamp = log.ExecutedAt.ToString("yyyy-MM-dd HH:mm:ss");
            var guild = EscapeCsvField(log.GuildName ?? "Direct Message");
            var user = EscapeCsvField(log.Username ?? "Unknown");
            var command = EscapeCsvField(log.CommandName);
            var status = log.Success ? "Success" : "Failed";
            var error = EscapeCsvField(log.ErrorMessage ?? "");

            csv.AppendLine($"{timestamp},{command},{guild},{user},{log.ResponseTimeMs},{status},{error}");
        }

        _logger.LogInformation("Exported {Count} command logs to CSV", result.Items.Count);

        var fileName = $"command-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", fileName);
    }

    private static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }
}
