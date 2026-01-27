using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

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
    private readonly IGuildMemberService _guildMemberService;
    private readonly IGuildAudioSettingsService _guildAudioSettingsService;
    private readonly ISoundRepository _soundRepository;
    private readonly ITtsMessageRepository _ttsMessageRepository;
    private readonly IAssistantGuildSettingsService _assistantGuildSettingsService;
    private readonly AssistantOptions _assistantOptions;
    private readonly ILogger<DetailsModel> _logger;

    private const int RecentCommandsLimit = 10;

    public DetailsModel(
        IGuildService guildService,
        ICommandLogService commandLogService,
        IWelcomeService welcomeService,
        IScheduledMessageService scheduledMessageService,
        IRatWatchService ratWatchService,
        IReminderRepository reminderRepository,
        IGuildMemberService guildMemberService,
        IGuildAudioSettingsService guildAudioSettingsService,
        ISoundRepository soundRepository,
        ITtsMessageRepository ttsMessageRepository,
        IAssistantGuildSettingsService assistantGuildSettingsService,
        IOptions<AssistantOptions> assistantOptions,
        ILogger<DetailsModel> logger)
    {
        _guildService = guildService;
        _commandLogService = commandLogService;
        _welcomeService = welcomeService;
        _scheduledMessageService = scheduledMessageService;
        _ratWatchService = ratWatchService;
        _reminderRepository = reminderRepository;
        _guildMemberService = guildMemberService;
        _guildAudioSettingsService = guildAudioSettingsService;
        _soundRepository = soundRepository;
        _ttsMessageRepository = ttsMessageRepository;
        _assistantGuildSettingsService = assistantGuildSettingsService;
        _assistantOptions = assistantOptions.Value;
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
    /// Gets the top leaderboard entries for this guild (up to 5).
    /// </summary>
    public List<RatLeaderboardEntryDto> TopRatLeaderboard { get; set; } = new();

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

    /// <summary>
    /// Gets the upcoming reminders for this guild (up to 5).
    /// </summary>
    public List<UpcomingReminderDto> UpcomingReminders { get; set; } = new();

    /// <summary>
    /// Gets the total count of guild members.
    /// </summary>
    public int MembersTotalCount { get; set; }

    /// <summary>
    /// Gets the count of members active today.
    /// </summary>
    public int MembersActiveToday { get; set; }

    /// <summary>
    /// Gets the newest 5 members who joined the guild.
    /// </summary>
    public List<GuildMemberDto> NewestMembers { get; set; } = new();

    /// <summary>
    /// Gets whether audio is enabled for this guild.
    /// </summary>
    public bool AudioEnabled { get; set; }

    /// <summary>
    /// Gets the total count of sounds for this guild.
    /// </summary>
    public int TotalSoundCount { get; set; }

    /// <summary>
    /// Gets the top sounds by play count this week.
    /// </summary>
    public List<(string Name, int PlayCount)> TopSounds { get; set; } = new();

    /// <summary>
    /// Gets the most used TTS voice this week.
    /// </summary>
    public string? MostUsedTtsVoice { get; set; }

    /// <summary>
    /// Gets whether the assistant is globally enabled.
    /// </summary>
    public bool AssistantGloballyEnabled { get; set; }

    /// <summary>
    /// Gets whether the assistant is enabled for this guild.
    /// </summary>
    public bool AssistantLocallyEnabled { get; set; }

    /// <summary>
    /// Gets the count of allowed channels (0 means all channels).
    /// </summary>
    public int AssistantChannelCount { get; set; }

    /// <summary>
    /// Gets the rate limit for this guild.
    /// </summary>
    public int AssistantRateLimit { get; set; }

    /// <summary>
    /// Gets whether the rate limit is a guild override (true) or global default (false).
    /// </summary>
    public bool AssistantIsRateLimitOverride { get; set; }

    /// <summary>
    /// Gets the rate limit window in minutes.
    /// </summary>
    public int AssistantRateLimitWindowMinutes { get; set; }

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

        // Get leaderboard for top rats
        var leaderboard = await _ratWatchService.GetLeaderboardAsync(guildId, 5, cancellationToken);
        TopRatLeaderboard = leaderboard.ToList();

        // Fetch reminder stats
        var (remindersTotal, remindersPending, remindersDeliveredToday, remindersFailed) =
            await _reminderRepository.GetGuildStatsAsync(guildId, cancellationToken);
        RemindersTotal = remindersTotal;
        RemindersPending = remindersPending;
        RemindersDeliveredToday = remindersDeliveredToday;
        RemindersFailed = remindersFailed;

        // Fetch upcoming reminders
        UpcomingReminders = (await _reminderRepository.GetUpcomingAsync(guildId, 5, cancellationToken)).ToList();

        // Fetch member stats
        var memberCountQuery = new GuildMemberQueryDto { IsActive = true };
        MembersTotalCount = await _guildMemberService.GetMemberCountAsync(guildId, memberCountQuery, cancellationToken);

        // Fetch members active today
        var activeTodayQuery = new GuildMemberQueryDto
        {
            IsActive = true,
            LastActiveAtStart = DateTime.UtcNow.Date
        };
        MembersActiveToday = await _guildMemberService.GetMemberCountAsync(guildId, activeTodayQuery, cancellationToken);

        // Fetch newest 5 members
        var newestMembersQuery = new GuildMemberQueryDto
        {
            IsActive = true,
            SortBy = "JoinedAt",
            SortDescending = true,
            Page = 1,
            PageSize = 5
        };
        var newestMembersResponse = await _guildMemberService.GetMembersAsync(guildId, newestMembersQuery, cancellationToken);
        NewestMembers = newestMembersResponse.Items.ToList();

        // Fetch audio widget data
        var audioSettings = await _guildAudioSettingsService.GetSettingsAsync(guildId, cancellationToken);
        AudioEnabled = audioSettings?.AudioEnabled ?? false;

        // Fetch total sound count
        TotalSoundCount = await _soundRepository.GetSoundCountAsync(guildId, cancellationToken);

        // Fetch top sounds this week
        var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
        TopSounds = (await _soundRepository.GetTopSoundsByPlayCountAsync(guildId, 3, oneWeekAgo, cancellationToken)).ToList();

        // Fetch most used TTS voice this week
        MostUsedTtsVoice = await _ttsMessageRepository.GetMostUsedVoiceAsync(guildId, oneWeekAgo, cancellationToken);

        // Fetch assistant widget data
        AssistantGloballyEnabled = _assistantOptions.GloballyEnabled;
        var assistantSettings = await _assistantGuildSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);
        AssistantLocallyEnabled = assistantSettings.IsEnabled;
        AssistantChannelCount = assistantSettings.GetAllowedChannelIdsList().Count;
        AssistantIsRateLimitOverride = assistantSettings.RateLimitOverride.HasValue;
        AssistantRateLimit = assistantSettings.RateLimitOverride ?? _assistantOptions.DefaultRateLimit;
        AssistantRateLimitWindowMinutes = _assistantOptions.RateLimitWindowMinutes;

        _logger.LogDebug("Retrieved guild {GuildId} with {CommandCount} recent commands, WelcomeEnabled={WelcomeEnabled}, ScheduledMessages={ScheduledCount}, RatWatches={RatWatchCount}, Reminders={ReminderCount}, Members={MemberCount}, AudioEnabled={AudioEnabled}, Sounds={SoundCount}, AssistantEnabled={AssistantEnabled}",
            guildId, recentCommandsResponse.Items.Count, WelcomeEnabled, totalCount, ratWatchTotalCount, remindersTotal, MembersTotalCount, AudioEnabled, TotalSoundCount, AssistantLocallyEnabled);

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
