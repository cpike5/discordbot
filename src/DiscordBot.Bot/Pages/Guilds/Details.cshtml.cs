using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

// ReSharper disable MemberCanBePrivate.Global

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for displaying detailed guild information.
/// </summary>
[Authorize(Policy = "RequireModerator")]
public class DetailsModel : PageModel
{
    private readonly IGuildService _guildService;
    private readonly ICommandLogService _commandLogService;
    private readonly IWelcomeService _welcomeService;
    private readonly IScheduledMessageService _scheduledMessageService;
    private readonly IRatWatchService _ratWatchService;
    private readonly IReminderRepository _reminderRepository;
    private readonly ILogger<DetailsModel> _logger;

    private const int RecentCommandsLimit = 10;

    public DetailsModel(
        IGuildService guildService,
        ICommandLogService commandLogService,
        IWelcomeService welcomeService,
        IScheduledMessageService scheduledMessageService,
        IRatWatchService ratWatchService,
        IReminderRepository reminderRepository,
        ILogger<DetailsModel> logger)
    {
        _guildService = guildService;
        _commandLogService = commandLogService;
        _welcomeService = welcomeService;
        _scheduledMessageService = scheduledMessageService;
        _ratWatchService = ratWatchService;
        _reminderRepository = reminderRepository;
        _logger = logger;
    }

    /// <summary>
    /// Gets the view model containing guild details.
    /// </summary>
    public GuildDetailViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Gets whether welcome messages are enabled for this guild.
    /// </summary>
    public bool WelcomeEnabled { get; set; }

    /// <summary>
    /// Gets the welcome channel name if configured.
    /// </summary>
    public string? WelcomeChannelName { get; set; }

    /// <summary>
    /// Success message from TempData.
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Gets the total count of scheduled messages for this guild.
    /// </summary>
    public int ScheduledMessagesTotal { get; set; }

    /// <summary>
    /// Gets the count of active (enabled) scheduled messages.
    /// </summary>
    public int ScheduledMessagesActive { get; set; }

    /// <summary>
    /// Gets the count of paused (disabled) scheduled messages.
    /// </summary>
    public int ScheduledMessagesPaused { get; set; }

    /// <summary>
    /// Gets the next scheduled message execution time (UTC).
    /// </summary>
    public DateTime? NextScheduledExecution { get; set; }

    /// <summary>
    /// Gets the next scheduled execution time in ISO format for client-side timezone conversion.
    /// </summary>
    public string? NextScheduledExecutionUtcIso => NextScheduledExecution.HasValue
        ? DateTime.SpecifyKind(NextScheduledExecution.Value, DateTimeKind.Utc).ToString("o")
        : null;

    /// <summary>
    /// Gets the title of the next scheduled message.
    /// </summary>
    public string? NextScheduledMessageTitle { get; set; }

    /// <summary>
    /// Gets whether Rat Watch is enabled for this guild.
    /// </summary>
    public bool RatWatchEnabled { get; set; }

    /// <summary>
    /// Gets the total number of Rat Watches for this guild.
    /// </summary>
    public int RatWatchTotal { get; set; }

    /// <summary>
    /// Gets the count of pending Rat Watches.
    /// </summary>
    public int RatWatchPending { get; set; }

    /// <summary>
    /// Gets the count of completed Rat Watches.
    /// </summary>
    public int RatWatchCompleted { get; set; }

    /// <summary>
    /// Gets the top "rat" username for this guild (most guilty verdicts).
    /// </summary>
    public string? TopRatUsername { get; set; }

    /// <summary>
    /// Gets the guilty count for the top rat.
    /// </summary>
    public int TopRatGuiltyCount { get; set; }

    /// <summary>
    /// Gets the total number of reminders for this guild.
    /// </summary>
    public int RemindersTotal { get; set; }

    /// <summary>
    /// Gets the count of pending reminders.
    /// </summary>
    public int RemindersPending { get; set; }

    /// <summary>
    /// Gets the count of reminders delivered today.
    /// </summary>
    public int RemindersDeliveredToday { get; set; }

    /// <summary>
    /// Gets the count of failed reminders.
    /// </summary>
    public int RemindersFailed { get; set; }

    public async Task<IActionResult> OnGetAsync(ulong id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing guild details page for guild {GuildId}", id);

        // Fetch guild data
        var guild = await _guildService.GetGuildByIdAsync(id, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", id);
            return NotFound();
        }

        // Fetch recent command activity for this guild
        var commandQuery = new CommandLogQueryDto
        {
            GuildId = id,
            Page = 1,
            PageSize = RecentCommandsLimit
        };
        var recentCommandsResponse = await _commandLogService.GetLogsAsync(commandQuery, cancellationToken);

        // Fetch welcome configuration status
        var welcomeConfig = await _welcomeService.GetConfigurationAsync(id, cancellationToken);
        WelcomeEnabled = welcomeConfig?.IsEnabled ?? false;

        // Fetch scheduled messages summary
        var (scheduledMessages, totalCount) = await _scheduledMessageService.GetByGuildIdAsync(id, 1, 100, cancellationToken);
        var messagesList = scheduledMessages.ToList();

        ScheduledMessagesTotal = totalCount;
        ScheduledMessagesActive = messagesList.Count(m => m.IsEnabled);
        ScheduledMessagesPaused = messagesList.Count(m => !m.IsEnabled);

        // Find the next scheduled execution
        var nextMessage = messagesList
            .Where(m => m.IsEnabled && m.NextExecutionAt.HasValue && m.NextExecutionAt.Value > DateTime.UtcNow)
            .OrderBy(m => m.NextExecutionAt)
            .FirstOrDefault();

        if (nextMessage != null)
        {
            NextScheduledExecution = nextMessage.NextExecutionAt;
            NextScheduledMessageTitle = nextMessage.Title;
        }

        // Fetch Rat Watch summary
        var ratWatchSettings = await _ratWatchService.GetGuildSettingsAsync(id, cancellationToken);
        RatWatchEnabled = ratWatchSettings.IsEnabled;

        var (ratWatches, ratWatchTotalCount) = await _ratWatchService.GetByGuildAsync(id, 1, 100, cancellationToken);
        var ratWatchList = ratWatches.ToList();

        RatWatchTotal = ratWatchTotalCount;
        RatWatchPending = ratWatchList.Count(w => w.Status == RatWatchStatus.Pending || w.Status == RatWatchStatus.Voting);
        RatWatchCompleted = ratWatchList.Count(w => w.Status == RatWatchStatus.Guilty || w.Status == RatWatchStatus.NotGuilty);

        // Get leaderboard for top rat
        var leaderboard = await _ratWatchService.GetLeaderboardAsync(id, 1, cancellationToken);
        var topRat = leaderboard.FirstOrDefault();
        if (topRat != null)
        {
            TopRatUsername = topRat.Username;
            TopRatGuiltyCount = topRat.GuiltyCount;
        }

        // Fetch reminder stats
        var (remindersTotal, remindersPending, remindersDeliveredToday, remindersFailed) =
            await _reminderRepository.GetGuildStatsAsync(id, cancellationToken);
        RemindersTotal = remindersTotal;
        RemindersPending = remindersPending;
        RemindersDeliveredToday = remindersDeliveredToday;
        RemindersFailed = remindersFailed;

        _logger.LogDebug("Retrieved guild {GuildId} with {CommandCount} recent commands, WelcomeEnabled={WelcomeEnabled}, ScheduledMessages={ScheduledCount}, RatWatches={RatWatchCount}, Reminders={ReminderCount}",
            id, recentCommandsResponse.Items.Count, WelcomeEnabled, totalCount, ratWatchTotalCount, remindersTotal);

        // Build view model
        ViewModel = GuildDetailViewModel.FromDto(guild, recentCommandsResponse.Items);

        // TODO: Set CanEdit based on user's guild-specific permissions
        // For now, all moderators can view but edit capability depends on future authorization

        return Page();
    }

    /// <summary>
    /// Handles POST request to sync a single guild from Discord.
    /// </summary>
    public async Task<IActionResult> OnPostSyncAsync(ulong id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User requesting sync for guild {GuildId}", id);

        try
        {
            var success = await _guildService.SyncGuildAsync(id, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Successfully synced guild {GuildId}", id);

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = true, message = "Guild synced successfully" });
                }

                SuccessMessage = "Guild synced successfully";
                return RedirectToPage(new { id });
            }
            else
            {
                _logger.LogWarning("Failed to sync guild {GuildId} - guild not found in Discord", id);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = false, message = "Guild not found in Discord client" });
                }

                SuccessMessage = null;
                return RedirectToPage(new { id });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing guild {GuildId}", id);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { success = false, message = "An error occurred while syncing the guild" });
            }

            SuccessMessage = null;
            return RedirectToPage(new { id });
        }
    }
}
