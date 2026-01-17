using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
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
[Authorize(Policy = "GuildAccess")]
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
    /// Guild layout breadcrumb ViewModel.
    /// </summary>
    public GuildBreadcrumbViewModel Breadcrumb { get; set; } = new();

    /// <summary>
    /// Guild layout header ViewModel.
    /// </summary>
    public GuildHeaderViewModel Header { get; set; } = new();

    /// <summary>
    /// Guild layout navigation ViewModel.
    /// </summary>
    public GuildNavBarViewModel Navigation { get; set; } = new();

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

    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing guild details page for guild {GuildId}", guildId);

        // Fetch guild data
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        // Fetch recent command activity for this guild
        var commandQuery = new CommandLogQueryDto
        {
            GuildId = guildId,
            Page = 1,
            PageSize = RecentCommandsLimit
        };
        var recentCommandsResponse = await _commandLogService.GetLogsAsync(commandQuery, cancellationToken);

        // Fetch welcome configuration status
        var welcomeConfig = await _welcomeService.GetConfigurationAsync(guildId, cancellationToken);
        WelcomeEnabled = welcomeConfig?.IsEnabled ?? false;

        // Fetch scheduled messages summary
        var (scheduledMessages, totalCount) = await _scheduledMessageService.GetByGuildIdAsync(guildId, 1, 100, cancellationToken);
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
        var ratWatchSettings = await _ratWatchService.GetGuildSettingsAsync(guildId, cancellationToken);
        RatWatchEnabled = ratWatchSettings.IsEnabled;

        var (ratWatches, ratWatchTotalCount) = await _ratWatchService.GetByGuildAsync(guildId, 1, 100, cancellationToken);
        var ratWatchList = ratWatches.ToList();

        RatWatchTotal = ratWatchTotalCount;
        RatWatchPending = ratWatchList.Count(w => w.Status == RatWatchStatus.Pending || w.Status == RatWatchStatus.Voting);
        RatWatchCompleted = ratWatchList.Count(w => w.Status == RatWatchStatus.Guilty || w.Status == RatWatchStatus.NotGuilty);

        // Get leaderboard for top rat
        var leaderboard = await _ratWatchService.GetLeaderboardAsync(guildId, 1, cancellationToken);
        var topRat = leaderboard.FirstOrDefault();
        if (topRat != null)
        {
            TopRatUsername = topRat.Username;
            TopRatGuiltyCount = topRat.GuiltyCount;
        }

        // Fetch reminder stats
        var (remindersTotal, remindersPending, remindersDeliveredToday, remindersFailed) =
            await _reminderRepository.GetGuildStatsAsync(guildId, cancellationToken);
        RemindersTotal = remindersTotal;
        RemindersPending = remindersPending;
        RemindersDeliveredToday = remindersDeliveredToday;
        RemindersFailed = remindersFailed;

        _logger.LogDebug("Retrieved guild {GuildId} with {CommandCount} recent commands, WelcomeEnabled={WelcomeEnabled}, ScheduledMessages={ScheduledCount}, RatWatches={RatWatchCount}, Reminders={ReminderCount}",
            guildId, recentCommandsResponse.Items.Count, WelcomeEnabled, totalCount, ratWatchTotalCount, remindersTotal);

        // Build view model
        ViewModel = GuildDetailViewModel.FromDto(guild, recentCommandsResponse.Items);

        // TODO: Set CanEdit based on user's guild-specific permissions
        // For now, all moderators can view but edit capability depends on future authorization

        // Populate guild layout ViewModels
        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, IsCurrent = true }
            }
        };

        Header = new GuildHeaderViewModel
        {
            GuildId = guild.Id,
            GuildName = guild.Name,
            GuildIconUrl = guild.IconUrl,
            PageTitle = guild.Name,
            PageDescription = $"ID: {guild.Id}",
            Actions = ViewModel.CanEdit ? new List<HeaderAction>
            {
                new()
                {
                    Label = "Active",
                    Url = "#",
                    Style = HeaderActionStyle.Secondary
                },
                new()
                {
                    Label = "Sync",
                    Url = "#",
                    Icon = "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15",
                    Style = HeaderActionStyle.Secondary
                },
                new()
                {
                    Label = "Members",
                    Url = $"/Guilds/Members?guildId={guild.Id}",
                    Icon = "M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z",
                    Style = HeaderActionStyle.Secondary
                },
                new()
                {
                    Label = "Edit Settings",
                    Url = $"/Guilds/Edit?id={guild.Id}",
                    Icon = "M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z",
                    Style = HeaderActionStyle.Primary
                }
            } : null
        };

        Navigation = new GuildNavBarViewModel
        {
            GuildId = guild.Id,
            ActiveTab = "overview",
            Tabs = GuildNavigationConfig.GetTabs().ToList()
        };

        return Page();
    }

    /// <summary>
    /// Handles POST request to sync a single guild from Discord.
    /// </summary>
    public async Task<IActionResult> OnPostSyncAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User requesting sync for guild {GuildId}", guildId);

        try
        {
            var success = await _guildService.SyncGuildAsync(guildId, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Successfully synced guild {GuildId}", guildId);

                // Check if this is an AJAX request
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = true, message = "Guild synced successfully" });
                }

                SuccessMessage = "Guild synced successfully";
                return RedirectToPage(new { guildId });
            }
            else
            {
                _logger.LogWarning("Failed to sync guild {GuildId} - guild not found in Discord", guildId);

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return new JsonResult(new { success = false, message = "Guild not found in Discord client" });
                }

                SuccessMessage = null;
                return RedirectToPage(new { guildId });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing guild {GuildId}", guildId);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { success = false, message = "An error occurred while syncing the guild" });
            }

            SuccessMessage = null;
            return RedirectToPage(new { guildId });
        }
    }
}
