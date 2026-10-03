using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.Reminders;

/// <summary>
/// Page model for the Reminders admin page.
/// Displays reminders for a guild with pagination and filtering.
/// </summary>
[Authorize(Policy = "GuildAccess")]
public class IndexModel : GuildPageModelBase
{
    private readonly IReminderRepository _reminderRepository;
    private readonly IGuildService _guildService;
    private readonly IDiscordUserResolver _userResolver;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IReminderRepository reminderRepository,
        IGuildService guildService,
        IDiscordUserResolver userResolver,
        ILogger<IndexModel> logger)
    {
        _reminderRepository = reminderRepository;
        _guildService = guildService;
        _userResolver = userResolver;
        _logger = logger;
    }

    /// <summary>
    /// View model for display properties.
    /// </summary>
    public RemindersIndexViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Gets or sets the status filter.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ReminderStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the page number.
    /// </summary>
    [BindProperty(SupportsGet = true, Name = "pageNumber")]
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

        // Populate guild layout ViewModels
        PopulateGuildLayout(guild.Id, guild.Name, guild.IconUrl, "reminders",
            "Reminders", "View and manage scheduled reminders for this server");

        try
        {
            // Get reminders with pagination and filtering
            var (reminders, totalCount) = await _reminderRepository.GetByGuildAsync(
                ulongGuildId,
                CurrentPage,
                PageSize,
                Status,
                cancellationToken);

            var (totalStats, pendingStats, deliveredTodayStats, failedStats) =
                await _reminderRepository.GetGuildStatsAsync(ulongGuildId, cancellationToken);

            var reminderList = reminders.ToList();

            // One lookup for every distinct user on the page (cached by the resolver), not one
            // Discord request per row
            var users = await _userResolver.ResolveUsersAsync(reminderList.Select(r => r.UserId));

            var reminderItems = reminderList.Select(reminder =>
            {
                var item = ReminderItemViewModel.FromEntity(reminder);
                return users.TryGetValue(reminder.UserId, out var user)
                    ? item.WithUserInfo(UserDisplay.Name(user.Username), user.AvatarUrl)
                    : item.WithUserInfo(UserDisplay.UnknownName, null);
            }).ToList();

            ViewModel = new RemindersIndexViewModel
            {
                GuildId = ulongGuildId,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                Reminders = reminderItems,
                TotalCount = totalCount,
                Stats = new ReminderStatsViewModel
                {
                    TotalCount = totalStats,
                    PendingCount = pendingStats,
                    DeliveredTodayCount = deliveredTodayStats,
                    FailedCount = failedStats
                },
                CurrentPage = CurrentPage,
                PageSize = PageSize,
                StatusFilter = Status
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to load reminders for guild {GuildId}", ulongGuildId);
            ViewModel = new RemindersIndexViewModel
            {
                GuildId = ulongGuildId,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                CurrentPage = CurrentPage,
                PageSize = PageSize,
                StatusFilter = Status
            };
            ErrorMessage = "The reminders could not be loaded. Try again in a moment.";
        }

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
            TempData.SetErrorToast("Reminder not found.");
            return RedirectToPage(new { guildId, pageNumber = CurrentPage, PageSize, Status });
        }

        if (reminder.Status != ReminderStatus.Pending)
        {
            _logger.LogWarning("Cannot cancel reminder {ReminderId} - status is {Status}, not Pending",
                reminderId, reminder.Status);
            TempData.SetErrorToast("Only pending reminders can be cancelled.");
            return RedirectToPage(new { guildId, pageNumber = CurrentPage, PageSize, Status });
        }

        // Update status to cancelled
        reminder.Status = ReminderStatus.Cancelled;
        await _reminderRepository.UpdateAsync(reminder, cancellationToken);

        _logger.LogInformation("Successfully cancelled reminder {ReminderId}", reminderId);
        TempData.SetSuccessToast("Reminder cancelled successfully.");

        return RedirectToPage(new { guildId, pageNumber = CurrentPage, PageSize, Status });
    }
}
