using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

// ReSharper disable MemberCanBePrivate.Global

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for displaying detailed guild information.
/// </summary>
[Authorize(Policy = "RequireModerator")]
[Authorize(Policy = "GuildAccess")]
public class DetailsModel : GuildPageModelBase
{
    private readonly IGuildDetailsAggregator _guildDetailsAggregator;
    private readonly IGuildService _guildService;
    private readonly IGuildMembershipService _guildMembershipService;
    private readonly ILogger<DetailsModel> _logger;

    private const int RecentCommandsLimit = 10;

    public DetailsModel(
        IGuildDetailsAggregator guildDetailsAggregator,
        IGuildService guildService,
        IGuildMembershipService guildMembershipService,
        ILogger<DetailsModel> logger)
    {
        _guildDetailsAggregator = guildDetailsAggregator;
        _guildService = guildService;
        _guildMembershipService = guildMembershipService;
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

        var aggregate = await _guildDetailsAggregator.BuildAsync(guildId, RecentCommandsLimit, cancellationToken);
        if (aggregate == null)
        {
            return NotFound();
        }

        var guild = aggregate.Guild;

        WelcomeEnabled = aggregate.WelcomeEnabled;
        ScheduledMessagesTotal = aggregate.ScheduledMessagesTotal;
        ScheduledMessagesActive = aggregate.ScheduledMessagesActive;
        ScheduledMessagesPaused = aggregate.ScheduledMessagesPaused;
        NextScheduledExecution = aggregate.NextScheduledExecution;
        NextScheduledMessageTitle = aggregate.NextScheduledMessageTitle;

        RatWatchEnabled = aggregate.RatWatchEnabled;
        RatWatchTotal = aggregate.RatWatchTotal;
        RatWatchPending = aggregate.RatWatchPending;
        RatWatchCompleted = aggregate.RatWatchCompleted;
        TopRatLeaderboard = aggregate.TopRatLeaderboard.ToList();

        RemindersTotal = aggregate.RemindersTotal;
        RemindersPending = aggregate.RemindersPending;
        RemindersDeliveredToday = aggregate.RemindersDeliveredToday;
        RemindersFailed = aggregate.RemindersFailed;
        UpcomingReminders = aggregate.UpcomingReminders.ToList();

        MembersTotalCount = aggregate.MembersTotalCount;
        MembersActiveToday = aggregate.MembersActiveToday;
        NewestMembers = aggregate.NewestMembers.ToList();

        AudioEnabled = aggregate.AudioEnabled;
        TotalSoundCount = aggregate.TotalSoundCount;
        TopSounds = aggregate.TopSounds.ToList();
        MostUsedTtsVoice = aggregate.MostUsedTtsVoice;

        AssistantGloballyEnabled = aggregate.AssistantGloballyEnabled;
        AssistantLocallyEnabled = aggregate.AssistantLocallyEnabled;
        AssistantChannelCount = aggregate.AssistantChannelCount;
        AssistantIsRateLimitOverride = aggregate.AssistantIsRateLimitOverride;
        AssistantRateLimit = aggregate.AssistantRateLimit;
        AssistantRateLimitWindowMinutes = aggregate.AssistantRateLimitWindowMinutes;

        // Build view model
        ViewModel = GuildDetailViewModel.FromDto(guild, aggregate.RecentCommandLogs);

        // Set CanEdit based on user's actual guild and application permissions
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(currentUserId))
        {
            var isGuildAdmin = await _guildMembershipService.IsGuildAdminAsync(currentUserId, guildId, cancellationToken);
            var isAppAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
            ViewModel.CanEdit = isGuildAdmin || isAppAdmin;
        }

        // Populate guild layout ViewModels
        Breadcrumb = BuildBasicBreadcrumb(guild.Id, guild.Name);

        Header = BuildHeader(guild.Id, guild.Name, guild.IconUrl, guild.Name, $"ID: {guild.Id}");
        Header.StatusBadge = new BadgeViewModel
        {
            Text = guild.IsActive ? "Active" : "Inactive",
            Variant = guild.IsActive ? BadgeVariant.Success : BadgeVariant.Error,
            Style = BadgeStyle.Subtle,
            IconLeft = "M10 18a8 8 0 100-16 8 8 0 000 16z"
        };
        Header.Actions = ViewModel.CanEdit ? new List<HeaderAction>
        {
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
        } : null;

        Navigation = BuildNavigation(guild.Id, "overview");

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
