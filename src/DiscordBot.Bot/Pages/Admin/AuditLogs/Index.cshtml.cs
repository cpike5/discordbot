using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Utilities;
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
    private readonly IMessageLogRepository _messageLogRepository;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IAuditLogService auditLogService,
        IGuildService guildService,
        IMessageLogRepository messageLogRepository,
        ILogger<IndexModel> logger)
    {
        _auditLogService = auditLogService;
        _guildService = guildService;
        _messageLogRepository = messageLogRepository;
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

    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    [BindProperty(SupportsGet = true)]
    public string? UserTimezone { get; set; }

    // View data
    public AuditLogListViewModel ViewModel { get; set; } = new();
    public IReadOnlyList<GuildDto> AvailableGuilds { get; set; } = Array.Empty<GuildDto>();

    /// <summary>
    /// Display name for the selected actor (populated for autocomplete).
    /// </summary>
    public string? ActorDisplayName { get; set; }

    /// <summary>
    /// Handles GET requests - redirects to unified Logs page.
    /// </summary>
    public IActionResult OnGetAsync(CancellationToken cancellationToken)
    {
        return RedirectToPage("/Admin/Logs", new { tab = "audit" });
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

            // Convert date filters from user timezone to UTC (same as OnGetAsync)
            DateTime? queryStartDate = null;
            DateTime? queryEndDate = null;

            if (StartDate.HasValue)
            {
                var startOfDay = StartDate.Value.Date;
                queryStartDate = TimezoneHelper.ConvertToUtc(startOfDay, UserTimezone);
            }

            if (EndDate.HasValue)
            {
                var endOfDay = EndDate.Value.Date.AddDays(1).AddTicks(-1);
                queryEndDate = TimezoneHelper.ConvertToUtc(endOfDay, UserTimezone);
            }

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
