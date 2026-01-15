using Discord.WebSocket;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.Reminders;

/// <summary>
/// Page model for the Reminders admin page.
/// Displays reminders for a guild with pagination and filtering.
/// </summary>
[Authorize(Policy = "GuildAccess")]
public class IndexModel : PageModel
{
    private readonly IReminderRepository _reminderRepository;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IReminderRepository reminderRepository,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        ILogger<IndexModel> logger)
    {
        _reminderRepository = reminderRepository;
        _guildService = guildService;
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <summary>
    /// View model for display properties.
    /// </summary>
    public RemindersIndexViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Success message from TempData.
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Error message from TempData.
    /// </summary>
    [TempData]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the status filter.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ReminderStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the page number.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "page")]
    public int CurrentPage { get; set; } = 1;

    /// <summary>
    /// Gets or sets the page size.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 20;

    /// <summary>
    /// Handles GET requests to display the reminders page.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(long guildId, CancellationToken cancellationToken = default)
    {
        var ulongGuildId = (ulong)guildId;
        _logger.LogInformation("User accessing Reminders admin page for guild {GuildId}, page {Page}", ulongGuildId, CurrentPage);

        // Validate pagination parameters
        if (CurrentPage < 1) CurrentPage = 1;
        if (PageSize < 1 || PageSize > 100) PageSize = 20;

        // Get guild info
        var guild = await _guildService.GetGuildByIdAsync(ulongGuildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", ulongGuildId);
            return NotFound();
        }

        // Get reminders with pagination and filtering
        var (reminders, totalCount) = await _reminderRepository.GetByGuildAsync(
            ulongGuildId,
            CurrentPage,
            PageSize,
            Status,
            cancellationToken);

        // Get statistics
        var (totalStats, pendingStats, deliveredTodayStats, failedStats) =
            await _reminderRepository.GetGuildStatsAsync(ulongGuildId, cancellationToken);

        var stats = new ReminderStatsViewModel
        {
            TotalCount = totalStats,
            PendingCount = pendingStats,
            DeliveredTodayCount = deliveredTodayStats,
            FailedCount = failedStats
        };

        // Populate user information for each reminder
        var reminderItems = new List<ReminderItemViewModel>();
        var socketGuild = _discordClient.GetGuild(ulongGuildId);

        foreach (var reminder in reminders)
        {
            var item = ReminderItemViewModel.FromEntity(reminder);
            string username = $"Unknown ({reminder.UserId})";
            string? avatarUrl = null;

            // Try to get user info from Discord
            if (socketGuild != null)
            {
                var socketUser = socketGuild.GetUser(reminder.UserId);
                if (socketUser != null)
                {
                    username = socketUser.GlobalName ?? socketUser.Username;
                    avatarUrl = socketUser.GetAvatarUrl() ?? socketUser.GetDefaultAvatarUrl();
                }
                else
                {
                    // Try REST API as fallback
                    try
                    {
                        var restUser = await _discordClient.Rest.GetUserAsync(reminder.UserId);
                        if (restUser != null)
                        {
                            username = restUser.GlobalName ?? restUser.Username;
                            avatarUrl = restUser.GetAvatarUrl() ?? restUser.GetDefaultAvatarUrl();
                        }
                    }
                    catch
                    {
                        // User not found or API error - use default
                    }
                }
            }

            item = item.WithUserInfo(username, avatarUrl);
            reminderItems.Add(item);
        }

        // Build view model
        ViewModel = new RemindersIndexViewModel
        {
            GuildId = ulongGuildId,
            GuildName = guild.Name,
            GuildIconUrl = guild.IconUrl,
            Reminders = reminderItems,
            TotalCount = totalCount,
            Stats = stats,
            CurrentPage = CurrentPage,
            PageSize = PageSize,
            StatusFilter = Status
        };

        return Page();
    }

    /// <summary>
    /// Handles POST requests to cancel a reminder.
    /// </summary>
    public async Task<IActionResult> OnPostCancelAsync(
        long guildId,
        Guid reminderId,
        CancellationToken cancellationToken = default)
    {
        var ulongGuildId = (ulong)guildId;
        _logger.LogInformation("User attempting to cancel reminder {ReminderId} for guild {GuildId}",
            reminderId, ulongGuildId);

        // Get the reminder
        var reminder = await _reminderRepository.GetByIdAsync(reminderId, cancellationToken);
        if (reminder == null || reminder.GuildId != ulongGuildId)
        {
            _logger.LogWarning("Reminder {ReminderId} not found or doesn't belong to guild {GuildId}",
                reminderId, ulongGuildId);
            ErrorMessage = "Reminder not found.";
            return RedirectToPage(new { guildId, page = CurrentPage, PageSize, Status });
        }

        if (reminder.Status != ReminderStatus.Pending)
        {
            _logger.LogWarning("Cannot cancel reminder {ReminderId} - status is {Status}, not Pending",
                reminderId, reminder.Status);
            ErrorMessage = "Only pending reminders can be cancelled.";
            return RedirectToPage(new { guildId, page = CurrentPage, PageSize, Status });
        }

        // Update status to cancelled
        reminder.Status = ReminderStatus.Cancelled;
        await _reminderRepository.UpdateAsync(reminder, cancellationToken);

        _logger.LogInformation("Successfully cancelled reminder {ReminderId}", reminderId);
        SuccessMessage = "Reminder cancelled successfully.";

        return RedirectToPage(new { guildId, page = CurrentPage, PageSize, Status });
    }
}
