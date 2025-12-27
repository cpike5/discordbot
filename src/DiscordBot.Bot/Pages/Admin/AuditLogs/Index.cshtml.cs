using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text;

namespace DiscordBot.Bot.Pages.Admin.AuditLogs;

/// <summary>
/// Page model for listing and searching audit logs.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class IndexModel : PageModel
{
    private readonly IAuditLogService _auditLogService;
    private readonly IGuildService _guildService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IAuditLogService auditLogService,
        IGuildService guildService,
        ILogger<IndexModel> logger)
    {
        _auditLogService = auditLogService;
        _guildService = guildService;
        _logger = logger;
    }

    // Filter properties bound from query string
    [BindProperty(SupportsGet = true)]
    public AuditLogCategory? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public AuditLogAction? Action { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ActorId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TargetType { get; set; }

    [BindProperty(SupportsGet = true)]
    public ulong? GuildId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    // View data
    public AuditLogListViewModel ViewModel { get; set; } = new();
    public IReadOnlyList<GuildDto> AvailableGuilds { get; set; } = Array.Empty<GuildDto>();

    /// <summary>
    /// Handles GET requests to display the audit log list.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Set default date range to last 24 hours if no filters specified
            if (!StartDate.HasValue && !EndDate.HasValue && !Category.HasValue &&
                !Action.HasValue && string.IsNullOrEmpty(ActorId) &&
                string.IsNullOrEmpty(TargetType) && !GuildId.HasValue &&
                string.IsNullOrEmpty(SearchTerm))
            {
                StartDate = DateTime.UtcNow.AddDays(-1);
                EndDate = DateTime.UtcNow;
            }

            _logger.LogDebug("Loading audit logs with filters: Category={Category}, Action={Action}, ActorId={ActorId}, TargetType={TargetType}, GuildId={GuildId}, StartDate={StartDate}, EndDate={EndDate}, SearchTerm={SearchTerm}, Page={Page}, PageSize={PageSize}",
                Category, Action, ActorId, TargetType, GuildId, StartDate, EndDate, SearchTerm, CurrentPage, PageSize);

            // Load available guilds for filter dropdown
            AvailableGuilds = await _guildService.GetAllGuildsAsync(cancellationToken);

            // Adjust dates for proper filtering:
            // - StartDate: Use start of day in UTC
            // - EndDate: Use end of day (23:59:59.999) in UTC to include all events on that day
            var queryStartDate = StartDate?.Date.ToUniversalTime();
            var queryEndDate = EndDate?.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

            // Build query
            var query = new AuditLogQueryDto
            {
                Category = Category,
                Action = Action,
                ActorId = ActorId,
                TargetType = TargetType,
                GuildId = GuildId,
                StartDate = queryStartDate,
                EndDate = queryEndDate,
                SearchTerm = SearchTerm,
                Page = CurrentPage,
                PageSize = PageSize
            };

            // Get audit logs
            var (items, totalCount) = await _auditLogService.GetLogsAsync(query, cancellationToken);

            _logger.LogInformation("Retrieved {Count} audit logs (page {Page} of {TotalPages})",
                items.Count, CurrentPage, Math.Ceiling((double)totalCount / PageSize));

            // Build paginated response for view model
            var paginatedResponse = new PaginatedResponseDto<AuditLogDto>
            {
                Items = items,
                Page = CurrentPage,
                PageSize = PageSize,
                TotalCount = totalCount
            };

            // Build filter options
            var filters = new AuditLogFilterOptions
            {
                Category = Category,
                Action = Action,
                ActorId = ActorId,
                TargetType = TargetType,
                GuildId = GuildId,
                StartDate = StartDate,
                EndDate = EndDate,
                SearchTerm = SearchTerm
            };

            // Build view model
            ViewModel = AuditLogListViewModel.FromPaginatedDto(paginatedResponse, filters);

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading audit logs");
            TempData["Error"] = "An error occurred while loading audit logs. Please try again.";
            return Page();
        }
    }

    /// <summary>
    /// Handles export to CSV request.
    /// </summary>
    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Exporting audit logs with filters: Category={Category}, Action={Action}, ActorId={ActorId}, TargetType={TargetType}, GuildId={GuildId}, StartDate={StartDate}, EndDate={EndDate}, SearchTerm={SearchTerm}",
                Category, Action, ActorId, TargetType, GuildId, StartDate, EndDate, SearchTerm);

            // Adjust dates for proper filtering (same as OnGetAsync)
            var queryStartDate = StartDate?.Date.ToUniversalTime();
            var queryEndDate = EndDate?.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

            // Build query with no pagination (get all matching logs)
            var query = new AuditLogQueryDto
            {
                Category = Category,
                Action = Action,
                ActorId = ActorId,
                TargetType = TargetType,
                GuildId = GuildId,
                StartDate = queryStartDate,
                EndDate = queryEndDate,
                SearchTerm = SearchTerm,
                Page = 1,
                PageSize = int.MaxValue // Get all results for export
            };

            // Get all matching audit logs
            var (items, totalCount) = await _auditLogService.GetLogsAsync(query, cancellationToken);

            _logger.LogInformation("Exporting {Count} audit log entries to CSV", totalCount);

            // Generate CSV
            var csv = new StringBuilder();
            csv.AppendLine("Timestamp,Category,Action,Actor,Target Type,Target ID,Guild,Details,IP Address,Correlation ID");

            foreach (var log in items)
            {
                csv.AppendLine($"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\"," +
                    $"\"{EscapeCsv(log.CategoryName)}\"," +
                    $"\"{EscapeCsv(log.ActionName)}\"," +
                    $"\"{EscapeCsv(log.ActorDisplayName ?? log.ActorId ?? string.Empty)}\"," +
                    $"\"{EscapeCsv(log.TargetType ?? string.Empty)}\"," +
                    $"\"{EscapeCsv(log.TargetId ?? string.Empty)}\"," +
                    $"\"{EscapeCsv(log.GuildName ?? string.Empty)}\"," +
                    $"\"{EscapeCsv(log.Details ?? string.Empty)}\"," +
                    $"\"{EscapeCsv(log.IpAddress ?? string.Empty)}\"," +
                    $"\"{EscapeCsv(log.CorrelationId ?? string.Empty)}\"");
            }

            var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
            var bytes = Encoding.UTF8.GetBytes(csv.ToString());

            return File(bytes, "text/csv", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting audit logs to CSV");
            TempData["Error"] = "An error occurred while exporting audit logs. Please try again.";
            return RedirectToPage();
        }
    }

    /// <summary>
    /// Escapes CSV field values to prevent injection and formatting issues.
    /// </summary>
    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Escape double quotes by doubling them
        return value.Replace("\"", "\"\"");
    }
}
