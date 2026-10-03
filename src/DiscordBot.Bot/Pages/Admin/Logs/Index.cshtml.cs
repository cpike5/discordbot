using DiscordBot.Bot.Helpers;
using System.Text;
using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Admin.Logs;

/// <summary>
/// Unified page model for message logs and audit logs with tabbed navigation.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class IndexModel : PageModel
{
    private readonly IMessageLogService _messageLogService;
    private readonly IAuditLogService _auditLogService;
    private readonly IGuildService _guildService;
    private readonly IMessageLogRepository _messageLogRepository;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IMessageLogService messageLogService,
        IAuditLogService auditLogService,
        IGuildService guildService,
        IMessageLogRepository messageLogRepository,
        DiscordSocketClient discordClient,
        ILogger<IndexModel> logger)
    {
        _messageLogService = messageLogService;
        _auditLogService = auditLogService;
        _guildService = guildService;
        _messageLogRepository = messageLogRepository;
        _discordClient = discordClient;
        _logger = logger;
    }

    // Message Logs filter properties
    [BindProperty(SupportsGet = true)]
    public ulong? AuthorId { get; set; }

    [BindProperty(SupportsGet = true)]
    public ulong? MessageGuildId { get; set; }

    [BindProperty(SupportsGet = true)]
    public ulong? ChannelId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? MessageSource { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? MessageStartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? MessageEndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? MessageSearchTerm { get; set; }

    [BindProperty(SupportsGet = true, Name = "messagePageNumber")]
    public int MessageCurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int MessagePageSize { get; set; } = 25;

    // Audit Logs filter properties
    [BindProperty(SupportsGet = true)]
    public AuditLogCategory? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public AuditLogAction? Action { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ActorId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TargetType { get; set; }

    [BindProperty(SupportsGet = true)]
    public ulong? AuditGuildId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? AuditStartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? AuditEndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AuditSearchTerm { get; set; }

    [BindProperty(SupportsGet = true, Name = "auditPageNumber")]
    public int AuditCurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int AuditPageSize { get; set; } = 25;

    [BindProperty(SupportsGet = true)]
    public string? UserTimezone { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Tab { get; set; }

    // Display names for autocomplete fields
    public string? AuthorUsername { get; set; }
    public string? MessageGuildName { get; set; }
    public string? ChannelName { get; set; }
    public string? ActorDisplayName { get; set; }

    // View models for each tab
    public MessageLogListViewModel MessageLogsViewModel { get; set; } = new();
    public AuditLogListViewModel AuditLogsViewModel { get; set; } = new();
    public IReadOnlyList<GuildDto> AvailableGuilds { get; set; } = Array.Empty<GuildDto>();
    public string ActiveTab { get; set; } = "messages";

    /// <summary>True when no date range was asked for and the default (last 7 days) was applied.</summary>
    public bool MessageDatesDefaulted { get; private set; }

    /// <summary>True when no date range was asked for and the default (last 30 days) was applied.</summary>
    public bool AuditDatesDefaulted { get; private set; }

    /// <summary>
    /// Whether the user narrowed the message list. The default date range does not count, so a
    /// fresh visit with nothing logged is "nothing yet" and not "nothing matches your filters".
    /// </summary>
    public bool MessageHasUserFilters =>
        AuthorId.HasValue || MessageGuildId.HasValue || ChannelId.HasValue ||
        !string.IsNullOrEmpty(MessageSource) || !string.IsNullOrWhiteSpace(MessageSearchTerm) ||
        (!MessageDatesDefaulted && (MessageStartDate.HasValue || MessageEndDate.HasValue));

    /// <summary>Whether the user narrowed the audit list. The default date range does not count.</summary>
    public bool AuditHasUserFilters =>
        Category.HasValue || Action.HasValue || !string.IsNullOrWhiteSpace(ActorId) ||
        !string.IsNullOrWhiteSpace(TargetType) || AuditGuildId.HasValue ||
        !string.IsNullOrWhiteSpace(AuditSearchTerm) ||
        (!AuditDatesDefaulted && (AuditStartDate.HasValue || AuditEndDate.HasValue));

    /// <summary>
    /// Page-state error shown in place of the results when the active tab's logs failed to load.
    /// Plain page state, never TempData.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The address of this page as requested (path and query), used by Retry and as the
    /// <c>returnUrl</c> that detail pages send the user back to with the filters intact.
    /// </summary>
    public string CurrentUrl => $"{Request.Path}{Request.QueryString}";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        // Determine active tab (default to "messages"; anything unknown falls back to it)
        ActiveTab = string.Equals(Tab, "audit", StringComparison.OrdinalIgnoreCase) ? "audit" : "messages";

        _logger.LogDebug("Loading unified Logs page with active tab: {ActiveTab}", ActiveTab);

        try
        {
            // Tabs are separate page loads (?tab=), so only the active tab's data is read
            if (ActiveTab == "audit")
            {
                await LoadAuditLogsAsync(cancellationToken);
            }
            else
            {
                await LoadMessageLogsAsync(cancellationToken);
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading unified Logs page");
            ErrorMessage = ActiveTab == "audit"
                ? "The audit log could not be loaded. Try again in a moment."
                : "The message log could not be loaded. Try again in a moment.";
            return Page();
        }
    }

    private async Task LoadMessageLogsAsync(CancellationToken cancellationToken)
    {
        // Apply default date range (last 7 days) if no date filters specified
        if (!MessageStartDate.HasValue && !MessageEndDate.HasValue)
        {
            MessageStartDate = DateTime.UtcNow.Date.AddDays(-7);
            MessageEndDate = DateTime.UtcNow.Date.AddDays(1);
            MessageDatesDefaulted = true;
        }

        _logger.LogDebug("Loading message logs with filters: AuthorId={AuthorId}, GuildId={GuildId}, ChannelId={ChannelId}, Source={Source}, StartDate={StartDate}, EndDate={EndDate}, SearchTerm={SearchTerm}, Page={Page}, PageSize={PageSize}",
            AuthorId, MessageGuildId, ChannelId, MessageSource, MessageStartDate, MessageEndDate, MessageSearchTerm, MessageCurrentPage, MessagePageSize);

        // Parse source filter
        MessageSource? sourceFilter = null;
        if (!string.IsNullOrEmpty(MessageSource))
        {
            if (Enum.TryParse<MessageSource>(MessageSource, true, out var parsedSource))
            {
                sourceFilter = parsedSource;
            }
        }

        // Build query
        var query = new MessageLogQueryDto
        {
            AuthorId = AuthorId,
            GuildId = MessageGuildId,
            ChannelId = ChannelId,
            Source = sourceFilter,
            StartDate = MessageStartDate,
            EndDate = MessageEndDate,
            SearchTerm = MessageSearchTerm,
            Page = MessageCurrentPage,
            PageSize = MessagePageSize
        };

        // Get messages
        var result = await _messageLogService.GetLogsAsync(query, cancellationToken);

        _logger.LogInformation("Retrieved {Count} message logs (page {Page} of {TotalPages})",
            result.Items.Count, result.Page, result.TotalPages);

        // Populate display names for autocomplete fields
        await PopulateMessageDisplayNamesAsync(cancellationToken);

        // Build view model
        MessageLogsViewModel = new MessageLogListViewModel
        {
            Messages = result.Items,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            AuthorId = AuthorId,
            GuildId = MessageGuildId,
            ChannelId = ChannelId,
            Source = MessageSource,
            StartDate = MessageStartDate,
            EndDate = MessageEndDate,
            SearchTerm = MessageSearchTerm
        };
    }

    private async Task LoadAuditLogsAsync(CancellationToken cancellationToken)
    {
        // Default to last 30 days when no date filters specified
        if (!AuditStartDate.HasValue && !AuditEndDate.HasValue)
        {
            AuditStartDate = DateTime.UtcNow.Date.AddDays(-30);
            AuditEndDate = DateTime.UtcNow.Date.AddDays(1);
            AuditDatesDefaulted = true;
        }

        _logger.LogDebug("Loading audit logs with filters: Category={Category}, Action={Action}, ActorId={ActorId}, TargetType={TargetType}, GuildId={GuildId}, StartDate={StartDate}, EndDate={EndDate}, SearchTerm={SearchTerm}, Page={Page}, PageSize={PageSize}",
            Category, Action, ActorId, TargetType, AuditGuildId, AuditStartDate, AuditEndDate, AuditSearchTerm, AuditCurrentPage, AuditPageSize);

        // Load available guilds for filter dropdown
        AvailableGuilds = await _guildService.GetAllGuildsAsync(cancellationToken);

        // Populate actor display name for autocomplete
        if (!string.IsNullOrEmpty(ActorId) && ulong.TryParse(ActorId, out var actorUserId))
        {
            var userMessages = await _messageLogRepository.GetUserMessagesAsync(actorUserId, limit: 1, cancellationToken: cancellationToken);
            var message = userMessages.FirstOrDefault();
            ActorDisplayName = message?.User?.Username;
        }

        var query = BuildAuditQuery(AuditCurrentPage, AuditPageSize);

        // Get audit logs
        var (items, totalCount) = await _auditLogService.GetLogsAsync(query, cancellationToken);

        _logger.LogInformation("Retrieved {Count} audit logs (page {Page} of {TotalPages})",
            items.Count, AuditCurrentPage, Math.Ceiling((double)totalCount / AuditPageSize));

        // Build paginated response for view model
        var paginatedResponse = new PaginatedResponseDto<AuditLogDto>
        {
            Items = items,
            Page = AuditCurrentPage,
            PageSize = AuditPageSize,
            TotalCount = totalCount
        };

        // Build filter options
        var filters = new AuditLogFilterOptions
        {
            Category = Category,
            Action = Action,
            ActorId = ActorId,
            TargetType = TargetType,
            GuildId = AuditGuildId,
            StartDate = AuditStartDate,
            EndDate = AuditEndDate,
            SearchTerm = AuditSearchTerm
        };

        // Build view model
        AuditLogsViewModel = AuditLogListViewModel.FromPaginatedDto(paginatedResponse, filters);
    }

    private async Task PopulateMessageDisplayNamesAsync(CancellationToken cancellationToken)
    {
        // Get author username from message logs if AuthorId is specified
        if (AuthorId.HasValue)
        {
            var messages = await _messageLogRepository.GetUserMessagesAsync(
                AuthorId.Value,
                limit: 1,
                cancellationToken: cancellationToken);

            var message = messages.FirstOrDefault();
            AuthorUsername = message?.User?.Username;
        }

        // Get guild name if GuildId is specified
        if (MessageGuildId.HasValue)
        {
            var guild = await _guildService.GetGuildByIdAsync(MessageGuildId.Value);
            MessageGuildName = guild?.Name;
        }

        // Get channel name if ChannelId is specified
        if (ChannelId.HasValue && MessageGuildId.HasValue)
        {
            var socketGuild = _discordClient.GetGuild(MessageGuildId.Value);
            var channel = socketGuild?.GetChannel(ChannelId.Value);
            ChannelName = channel?.Name;
        }
    }

    /// <summary>
    /// Rows the audit export writes at most. A bigger result is cut off here, and the file name
    /// says so, rather than holding every row in memory or timing out.
    /// </summary>
    internal const int ExportRowCap = 10_000;

    /// <summary>The audit log service clamps a page to 100 rows, so the export reads in pages of that size.</summary>
    private const int ExportPageSize = 100;

    /// <summary>
    /// Streams the audit entries that match the current filters as CSV, a page at a time, so the
    /// whole result never sits in memory. Time columns are UTC and say so (UX decision D6).
    /// </summary>
    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        // The first page is read before the response starts, so a failure can still become an
        // error toast and a redirect instead of a broken download.
        var query = BuildAuditQuery(page: 1, pageSize: ExportPageSize);
        IReadOnlyList<AuditLogDto> items;
        int totalCount;
        try
        {
            _logger.LogInformation("Exporting audit logs with filters: Category={Category}, Action={Action}, ActorId={ActorId}, TargetType={TargetType}, GuildId={GuildId}, StartDate={StartDate}, EndDate={EndDate}, SearchTerm={SearchTerm}",
                Category, Action, ActorId, TargetType, AuditGuildId, AuditStartDate, AuditEndDate, AuditSearchTerm);

            (items, totalCount) = await _auditLogService.GetLogsAsync(query, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error exporting audit logs to CSV");
            TempData.SetErrorToast("The audit log could not be exported. Try again in a moment.");
            return RedirectToPage(new { tab = "audit" });
        }

        var truncated = totalCount > ExportRowCap;
        var fileName = $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}{(truncated ? $"-first-{ExportRowCap}" : string.Empty)}.csv";
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";

        try
        {
            await using var writer = new StreamWriter(Response.Body, new UTF8Encoding(false), 4096, leaveOpen: true);
            await writer.WriteLineAsync("Timestamp (UTC),Category,Action,Actor,Target Type,Target ID,Guild,Details,IP Address,Correlation ID");

            var written = 0;
            while (true)
            {
                foreach (var log in items)
                {
                    if (written >= ExportRowCap)
                    {
                        break;
                    }

                    await writer.WriteLineAsync(FormatExportRow(log));
                    written++;
                }

                await writer.FlushAsync(cancellationToken);

                if (written >= ExportRowCap || written >= totalCount || items.Count < ExportPageSize)
                {
                    break;
                }

                query.Page++;
                (items, totalCount) = await _auditLogService.GetLogsAsync(query, cancellationToken);
                if (items.Count == 0)
                {
                    break;
                }
            }

            _logger.LogInformation("Exported {Count} of {Total} audit log entries to CSV", written, totalCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The download has started, so a redirect is no longer possible. Drop the connection:
            // the browser then reports a failed download instead of keeping a file that looks
            // complete and is not.
            _logger.LogError(ex, "Error streaming the audit log export");
            HttpContext.Abort();
        }

        return new EmptyResult();
    }

    /// <summary>
    /// Builds the audit query from the bound filters, converting the date range from the user's
    /// time zone to UTC.
    /// </summary>
    private AuditLogQueryDto BuildAuditQuery(int page, int pageSize)
    {
        DateTime? queryStartDate = null;
        DateTime? queryEndDate = null;

        if (AuditStartDate.HasValue)
        {
            var startOfDay = AuditStartDate.Value.Date;
            queryStartDate = TimezoneHelper.ConvertToUtc(startOfDay, UserTimezone);
            _logger.LogDebug("Converted StartDate from {LocalDate} in {Timezone} to {UtcDate} UTC",
                startOfDay, UserTimezone ?? "UTC", queryStartDate);
        }

        if (AuditEndDate.HasValue)
        {
            var endOfDay = AuditEndDate.Value.Date.AddDays(1).AddTicks(-1);
            queryEndDate = TimezoneHelper.ConvertToUtc(endOfDay, UserTimezone);
            _logger.LogDebug("Converted EndDate from {LocalDate} in {Timezone} to {UtcDate} UTC",
                endOfDay, UserTimezone ?? "UTC", queryEndDate);
        }

        return new AuditLogQueryDto
        {
            Category = Category,
            Action = Action,
            ActorId = ActorId,
            TargetType = TargetType,
            GuildId = AuditGuildId,
            StartDate = queryStartDate,
            EndDate = queryEndDate,
            SearchTerm = AuditSearchTerm,
            Page = page,
            PageSize = pageSize
        };
    }

    private static string FormatExportRow(AuditLogDto log) =>
        $"\"{log.Timestamp:yyyy-MM-dd HH:mm:ss}\"," +
        $"\"{EscapeCsv(log.CategoryName)}\"," +
        $"\"{EscapeCsv(log.ActionName)}\"," +
        $"\"{EscapeCsv(log.ActorDisplayName ?? log.ActorId ?? string.Empty)}\"," +
        $"\"{EscapeCsv(log.TargetType ?? string.Empty)}\"," +
        $"\"{EscapeCsv(log.TargetId ?? string.Empty)}\"," +
        $"\"{EscapeCsv(log.GuildName ?? string.Empty)}\"," +
        $"\"{EscapeCsv(log.Details ?? string.Empty)}\"," +
        $"\"{EscapeCsv(log.IpAddress ?? string.Empty)}\"," +
        $"\"{EscapeCsv(log.CorrelationId ?? string.Empty)}\"";

    /// <summary>
    /// Escapes CSV field values to prevent injection and formatting issues.
    /// </summary>
    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Neutralize formula prefixes, then escape double quotes by doubling them
        return CsvField.NeutralizeFormula(value).Replace("\"", "\"\"");
    }
}
