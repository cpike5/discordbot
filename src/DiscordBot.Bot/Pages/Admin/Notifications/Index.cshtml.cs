using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;

namespace DiscordBot.Bot.Pages.Admin.Notifications;

/// <summary>
/// Page model for displaying notification history with filtering, pagination, and bulk actions.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class IndexModel : PageModel
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
    /// Current page number (1-based).
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Number of items per page.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 25;

    /// <summary>
    /// The view model containing notification list data.
    /// </summary>
    public NotificationListViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Available guilds for the filter dropdown.
    /// </summary>
    public IReadOnlyList<GuildDto> AvailableGuilds { get; set; } = Array.Empty<GuildDto>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        _logger.LogInformation("User {UserId} accessing notification history. Type={Type}, IsRead={IsRead}, Page={Page}",
            userId, Type, IsRead, CurrentPage);

        // Load guilds for dropdown
        AvailableGuilds = await _guildService.GetAllGuildsAsync(cancellationToken);

        // Set default date filter to last 7 days if no filters are provided
        if (!HasAnyFilters())
        {
            StartDate = DateTime.UtcNow.AddDays(-7);
            EndDate = DateTime.UtcNow;
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
            StartDate = StartDate,
            EndDate = EndDate,
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
