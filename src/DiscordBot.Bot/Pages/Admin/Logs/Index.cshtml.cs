using System.Text;
using Discord.WebSocket;
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

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        // Determine active tab (default to "messages")
        ActiveTab = string.IsNullOrWhiteSpace(Tab) ? "messages" : Tab.ToLowerInvariant();

        _logger.LogDebug("Loading unified Logs page with active tab: {ActiveTab}", ActiveTab);

        try
        {
            // Only load data for the active tab
            if (ActiveTab == "messages")
            {
                await LoadMessageLogsAsync(cancellationToken);
            }
            else if (ActiveTab == "audit")
            {
                await LoadAuditLogsAsync(cancellationToken);
            }

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading unified Logs page");
            TempData["Error"] = "An error occurred while loading logs. Please try again.";
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
        // Set default date range to last 24 hours if no filters specified
        if (!AuditStartDate.HasValue && !AuditEndDate.HasValue && !Category.HasValue &&
            !Action.HasValue && string.IsNullOrEmpty(ActorId) &&
            string.IsNullOrEmpty(TargetType) && !AuditGuildId.HasValue &&
            string.IsNullOrEmpty(AuditSearchTerm))
        {
            AuditStartDate = DateTime.UtcNow.AddDays(-1);
            AuditEndDate = DateTime.UtcNow;
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

        // Convert date filters from user timezone to UTC
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

        // Build query
        var query = new AuditLogQueryDto
        {
            Category = Category,
            Action = Action,
            ActorId = ActorId,
            TargetType = TargetType,
            GuildId = AuditGuildId,
            StartDate = queryStartDate,
            EndDate = queryEndDate,
            SearchTerm = AuditSearchTerm,
            Page = AuditCurrentPage,
            PageSize = AuditPageSize
        };

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

    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Exporting audit logs with filters: Category={Category}, Action={Action}, ActorId={ActorId}, TargetType={TargetType}, GuildId={GuildId}, StartDate={StartDate}, EndDate={EndDate}, SearchTerm={SearchTerm}",
                Category, Action, ActorId, TargetType, AuditGuildId, AuditStartDate, AuditEndDate, AuditSearchTerm);

            // Convert date filters from user timezone to UTC (same as LoadAuditLogsAsync)
            DateTime? queryStartDate = null;
            DateTime? queryEndDate = null;

            if (AuditStartDate.HasValue)
            {
                var startOfDay = AuditStartDate.Value.Date;
                queryStartDate = TimezoneHelper.ConvertToUtc(startOfDay, UserTimezone);
            }

            if (AuditEndDate.HasValue)
            {
                var endOfDay = AuditEndDate.Value.Date.AddDays(1).AddTicks(-1);
                queryEndDate = TimezoneHelper.ConvertToUtc(endOfDay, UserTimezone);
            }

            // Build query with no pagination (get all matching logs)
            var query = new AuditLogQueryDto
            {
                Category = Category,
                Action = Action,
                ActorId = ActorId,
                TargetType = TargetType,
                GuildId = AuditGuildId,
                StartDate = queryStartDate,
                EndDate = queryEndDate,
                SearchTerm = AuditSearchTerm,
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
