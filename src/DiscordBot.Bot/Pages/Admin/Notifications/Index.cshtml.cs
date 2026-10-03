using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DiscordBot.Bot.Pages.Admin.Notifications;

/// <summary>
/// Page model for displaying notification history with filtering, pagination, and bulk actions.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class IndexModel : PaginatedPageModel
{
    private readonly INotificationService _notificationService;
    private readonly IGuildService _guildService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        INotificationService notificationService,
        IGuildService guildService,
        ILogger<IndexModel> logger)
    {
        _notificationService = notificationService;
        _guildService = guildService;
        _logger = logger;

        PageSize = 25;
    }

    /// <summary>
    /// Filter by notification type.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public NotificationType? Type { get; set; }

    /// <summary>
    /// Filter by read status. True = read only, False = unread only, Null = all.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public bool? IsRead { get; set; }

    /// <summary>
    /// Filter by severity.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public AlertSeverity? Severity { get; set; }

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
    /// Search term for filtering by title/message.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by guild ID.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong? GuildId { get; set; }

    /// <summary>
    /// Show notifications of every date. Set by clearing the "Last 7 days" chip; without it, a
    /// list with no filters at all shows the last 7 days.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public bool AllTime { get; set; }

    /// <summary>
    /// True when this page applied the default "Last 7 days" window because nothing else was
    /// asked for. The chip that shows it, and the links that must not freeze it into explicit
    /// dates, key off this.
    /// </summary>
    public bool DefaultWindowApplied { get; private set; }

    /// <summary>
    /// The view model containing notification list data.
    /// </summary>
    public NotificationListViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Available guilds for the filter dropdown.
    /// </summary>
    public IReadOnlyList<GuildDto> AvailableGuilds { get; set; } = Array.Empty<GuildDto>();

    /// <summary>
    /// When this request began, in UTC. Taken before the list is read, so a notification created
    /// after it cannot be on the page; "Delete all" sends it as the upper bound of what to delete.
    /// </summary>
    public DateTime RenderedAtUtc { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        RenderedAtUtc = DateTime.UtcNow;

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        _logger.LogInformation("User {UserId} accessing notification history. Type={Type}, IsRead={IsRead}, Page={Page}",
            userId, Type, IsRead, CurrentPage);

        // Load guilds for dropdown
        AvailableGuilds = await _guildService.GetAllGuildsAsync(cancellationToken);

        // A list with no filters at all shows the last 7 days (decision D14). The window is whole
        // days, so "Delete all" on this list removes exactly what it shows. AllTime opts out.
        if (!HasAnyFilters() && !AllTime)
        {
            StartDate = DateTime.UtcNow.Date.AddDays(-7);
            EndDate = DateTime.UtcNow.Date;
            DefaultWindowApplied = true;
        }

        var query = new NotificationQueryDto
        {
            Type = Type,
            IsRead = IsRead,
            Severity = Severity,
            StartDate = StartDate,
            EndDate = EndDate?.Date.AddDays(1).AddTicks(-1), // End of day
            SearchTerm = SearchTerm,
            GuildId = GuildId,
            Page = CurrentPage,
            PageSize = PageSize
        };

        var result = await _notificationService.GetUserNotificationsPagedAsync(
            userId, query, cancellationToken);

        var filters = new NotificationFilterOptions
        {
            Type = Type,
            IsRead = IsRead,
            Severity = Severity,
            // The default window is not a filter the user chose; it has its own chip
            StartDate = DefaultWindowApplied ? null : StartDate,
            EndDate = DefaultWindowApplied ? null : EndDate,
            SearchTerm = SearchTerm,
            GuildId = GuildId
        };

        ViewModel = NotificationListViewModel.FromPaginatedDto(result, filters);

        return Page();
    }

    private bool HasAnyFilters() =>
        Type.HasValue || IsRead.HasValue || Severity.HasValue ||
        StartDate.HasValue || EndDate.HasValue ||
        !string.IsNullOrWhiteSpace(SearchTerm) || GuildId.HasValue;
}
