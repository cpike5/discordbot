using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Components.Enums;
using static DiscordBot.Bot.ViewModels.Components.Enums.ServerConnectionStatus;

namespace DiscordBot.Bot.Pages;

/// <summary>
/// Dashboard page for authenticated users.
/// Anonymous users are redirected to the public landing page via DashboardAnonymousRedirectFilter.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IBotService _botService;
    private readonly IGuildService _guildService;
    private readonly ICommandLogService _commandLogService;
    private readonly IAuditLogService _auditLogService;
    private readonly IVersionService _versionService;
    private readonly IRatWatchService _ratWatchService;
    private readonly IConnectionStateService _connectionStateService;

    public BotStatusViewModel BotStatus { get; private set; } = default!;
    public GuildStatsViewModel GuildStats { get; private set; } = default!;
    public CommandStatsViewModel CommandStats { get; private set; } = default!;
    public RecentActivityViewModel RecentActivity { get; private set; } = default!;
    public QuickActionsCardViewModel QuickActions { get; private set; } = default!;
    public AuditLogCardViewModel? AuditLog { get; private set; }

    // Dashboard Redesign ViewModels
    public BotStatusBannerViewModel BotStatusBanner { get; private set; } = default!;
    public List<HeroMetricCardViewModel> HeroMetrics { get; private set; } = new();
    public ActivityFeedTimelineViewModel ActivityTimeline { get; private set; } = default!;
    public ConnectedServersWidgetViewModel ConnectedServers { get; private set; } = default!;

    public IndexModel(
        ILogger<IndexModel> logger,
        IBotService botService,
        IGuildService guildService,
        ICommandLogService commandLogService,
        IAuditLogService auditLogService,
        IVersionService versionService,
        IRatWatchService ratWatchService,
        IConnectionStateService connectionStateService)
    {
        _logger = logger;
        _botService = botService;
        _guildService = guildService;
        _commandLogService = commandLogService;
        _auditLogService = auditLogService;
        _versionService = versionService;
        _ratWatchService = ratWatchService;
        _connectionStateService = connectionStateService;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        // Authorization is handled by [Authorize(Policy = "RequireViewer")] attribute
        // Anonymous users are redirected to /Landing by DashboardAnonymousRedirectFilter
        _logger.LogDebug("Dashboard accessed by authenticated user {UserId}", User.Identity?.Name);

        var statusDto = _botService.GetStatus();
        BotStatus = BotStatusViewModel.FromDto(statusDto);

        _logger.LogTrace("Bot status retrieved: {ConnectionState}, Latency: {LatencyMs}ms, Guilds: {GuildCount}",
            BotStatus.ConnectionState, BotStatus.LatencyMs, BotStatus.GuildCount);

        // Determine admin status for conditional audit log fetch
        var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

        // Retrieve data sequentially to avoid DbContext concurrency issues
        // DbContext is not thread-safe and is scoped per request
        var since = DateTime.UtcNow.AddHours(-24);
        var todayStart = DateTime.UtcNow.Date;

        var guilds = await _guildService.GetAllGuildsAsync();
        var commandStats = await _commandLogService.GetCommandStatsAsync(since);
        var recentLogsResponse = await _commandLogService.GetLogsAsync(new CommandLogQueryDto
        {
            Page = 1,
            PageSize = 10
        });
        var commandCountsByGuild = await _commandLogService.GetCommandCountsByGuildAsync(todayStart);
        var ratWatchActivity = await _ratWatchService.GetRecentActivityAsync(10);
        (IReadOnlyList<AuditLogDto> Items, int TotalCount)? auditLogsResponse = null;
        if (isAdmin)
        {
            auditLogsResponse = await _auditLogService.GetLogsAsync(new AuditLogQueryDto
            {
                Page = 1,
                PageSize = 5
            });
        }

        GuildStats = GuildStatsViewModel.FromGuilds(guilds);
        _logger.LogDebug("Guild stats retrieved: Total: {TotalGuilds}, Active: {ActiveGuilds}, Inactive: {InactiveGuilds}",
            GuildStats.TotalGuilds, GuildStats.ActiveGuilds, GuildStats.InactiveGuilds);

        CommandStats = CommandStatsViewModel.FromStats(commandStats, timeRangeHours: 24);
        _logger.LogDebug("Command stats retrieved: Total: {TotalCommands}, Top command: {TopCommand}",
            CommandStats.TotalCommands,
            CommandStats.TopCommands.FirstOrDefault()?.CommandName ?? "None");

        RecentActivity = RecentActivityViewModel.FromLogs(recentLogsResponse.Items);
        _logger.LogDebug("Recent activity retrieved: {ActivityCount} items",
            RecentActivity.Activities.Count);

        // Process audit logs if fetched
        if (auditLogsResponse.HasValue)
        {
            AuditLog = AuditLogCardViewModel.FromLogs(auditLogsResponse.Value.Items);
            _logger.LogDebug("Recent audit logs retrieved: {LogCount} items",
                AuditLog.Logs.Count);
        }

        // Build Quick Actions
        QuickActions = new QuickActionsCardViewModel
        {
            UserIsAdmin = isAdmin,
            Actions = new List<QuickActionItemViewModel>
            {
                new()
                {
                    Id = "restart-bot",
                    Label = "Restart Bot",
                    IconPath = "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15",
                    Color = QuickActionColor.Warning,
                    ActionType = QuickActionType.PostAction,
                    Handler = "RestartBot",
                    RequiresConfirmation = true,
                    ConfirmationModalId = "restartBotModal",
                    IsAdminOnly = true
                },
                new()
                {
                    Id = "sync-guilds",
                    Label = "Sync All Guilds",
                    IconPath = "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15",
                    Color = QuickActionColor.Blue,
                    ActionType = QuickActionType.PostAction,
                    Handler = "SyncAllGuilds",
                    RequiresConfirmation = false,
                    IsAdminOnly = false
                },
                new()
                {
                    Id = "settings",
                    Label = "Settings",
                    IconPath = "M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z",
                    Color = QuickActionColor.Gray,
                    ActionType = QuickActionType.Link,
                    Href = "/Admin/Settings",
                    IsAdminOnly = false
                }
            }
        };

        // Build Dashboard Redesign ViewModels
        BuildBotStatusBanner(statusDto, guilds);
        BuildHeroMetrics(guilds, CommandStats.TotalCommands);
        BuildActivityTimeline(recentLogsResponse.Items, ratWatchActivity);
        BuildConnectedServersWidget(guilds, commandCountsByGuild);

        return Page();
    }


    private void BuildBotStatusBanner(BotStatusDto statusDto, IEnumerable<GuildDto> guilds)
    {
        var guildList = guilds.ToList();
        var totalMembers = guildList.Sum(g => g.MemberCount ?? 0);
        BotStatusBanner = new BotStatusBannerViewModel
        {
            IsOnline = statusDto.ConnectionState == "Connected",
            StatusText = statusDto.ConnectionState == "Connected" ? "Bot is Online" : "Bot is Offline",
            ServerCount = guildList.Count,
            TotalMembers = totalMembers,
            UptimeDisplay = BotStatusViewModel.FormatUptime(statusDto.Uptime),
            Version = _versionService.GetVersion(),
            LatencyMs = statusDto.LatencyMs
        };
    }

    private void BuildHeroMetrics(IEnumerable<GuildDto> guilds, int commandsToday)
    {
        var guildList = guilds.ToList();
        var activeUsers = guildList.Where(g => g.IsActive).Sum(g => g.MemberCount ?? 0);

        // Get real uptime percentage from ConnectionStateService (24 hour period)
        var uptime24h = _connectionStateService.GetUptimePercentage(TimeSpan.FromHours(24));
        var uptimeDisplay = $"{uptime24h:F1}%";

        // SVG icon paths matching the prototype design
        const string serverIcon = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"2\" d=\"M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01\" />";
        const string usersIcon = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"2\" d=\"M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z\" />";
        const string commandIcon = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"2\" d=\"M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z\" />";
        const string uptimeIcon = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"2\" d=\"M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z\" />";

        HeroMetrics = new List<HeroMetricCardViewModel>
        {
            new() { Title = "Total Servers", Value = guildList.Count.ToString("N0"), TrendValue = "+0", TrendDirection = TrendDirection.Neutral, TrendLabel = "this week", AccentColor = CardAccent.Blue, IconSvg = serverIcon, ShowSparkline = false },
            new() { Title = "Active Users", Value = activeUsers.ToString("N0"), DataAttribute = "data-active-users", TrendValue = "+0", TrendDirection = TrendDirection.Neutral, TrendLabel = "today", AccentColor = CardAccent.Success, IconSvg = usersIcon, ShowSparkline = false },
            new() { Title = "Commands Today", Value = commandsToday.ToString("N0"), DataAttribute = "data-total-commands", TrendValue = "0%", TrendDirection = TrendDirection.Neutral, TrendLabel = "vs yesterday", AccentColor = CardAccent.Orange, IconSvg = commandIcon, ShowSparkline = false },
            new() { Title = "Uptime", Value = uptimeDisplay, DataAttribute = "data-uptime-24h", TrendValue = "", TrendDirection = TrendDirection.Up, TrendLabel = "last 24h", AccentColor = CardAccent.Info, IconSvg = uptimeIcon, ShowSparkline = false }
        };
    }

    private void BuildActivityTimeline(IEnumerable<CommandLogDto> recentLogs, IEnumerable<RatWatchDto> ratWatchActivity)
    {
        // Map command logs to activity feed items
        var commandItems = recentLogs.Select(log => new ActivityFeedItemViewModel
        {
            Type = log.Success ? ActivityItemType.Success : ActivityItemType.Error,
            Message = log.Success ? $"Command executed: /{log.CommandName}" : $"Command failed: /{log.CommandName}",
            CommandText = "/" + log.CommandName,
            Source = log.GuildName ?? "Unknown Server",
            Timestamp = log.ExecutedAt
        });

        // Map Rat Watch events to activity feed items
        var ratWatchItems = ratWatchActivity.Select(watch => new ActivityFeedItemViewModel
        {
            Type = MapRatWatchStatusToActivityType(watch.Status),
            Message = GetRatWatchMessage(watch),
            Source = watch.GuildName ?? "Unknown Server",
            Timestamp = GetRatWatchRelevantTimestamp(watch)
        });

        // Merge both sources, sort by timestamp descending, and take top 10
        var items = commandItems
            .Concat(ratWatchItems)
            .OrderByDescending(i => i.Timestamp)
            .Take(10)
            .ToList();

        _logger.LogDebug("Activity timeline built with {CommandCount} command logs and {RatWatchCount} Rat Watch events",
            recentLogs.Count(), ratWatchActivity.Count());

        ActivityTimeline = new ActivityFeedTimelineViewModel { Title = "Recent Activity", Items = items, ShowRefreshButton = true, ViewAllUrl = "/CommandLogs", MaxHeight = "400px" };
    }

    private static ActivityItemType MapRatWatchStatusToActivityType(Core.Enums.RatWatchStatus status)
    {
        return status switch
        {
            Core.Enums.RatWatchStatus.Pending => ActivityItemType.Warning,
            Core.Enums.RatWatchStatus.Voting => ActivityItemType.Info,
            Core.Enums.RatWatchStatus.Guilty => ActivityItemType.Error,
            Core.Enums.RatWatchStatus.NotGuilty => ActivityItemType.Success,
            Core.Enums.RatWatchStatus.ClearedEarly => ActivityItemType.Success,
            Core.Enums.RatWatchStatus.Cancelled => ActivityItemType.Info,
            Core.Enums.RatWatchStatus.Expired => ActivityItemType.Warning,
            _ => ActivityItemType.Info
        };
    }

    private static string GetRatWatchMessage(RatWatchDto watch)
    {
        return watch.Status switch
        {
            Core.Enums.RatWatchStatus.Pending => $"Rat Watch created for @{watch.AccusedUsername}",
            Core.Enums.RatWatchStatus.Voting => $"Rat Watch voting started for @{watch.AccusedUsername}",
            Core.Enums.RatWatchStatus.Guilty => $"Rat Watch verdict: Guilty (@{watch.AccusedUsername})",
            Core.Enums.RatWatchStatus.NotGuilty => $"Rat Watch verdict: Not Guilty (@{watch.AccusedUsername})",
            Core.Enums.RatWatchStatus.ClearedEarly => $"Rat Watch cleared early (@{watch.AccusedUsername})",
            Core.Enums.RatWatchStatus.Cancelled => $"Rat Watch cancelled (@{watch.AccusedUsername})",
            Core.Enums.RatWatchStatus.Expired => $"Rat Watch expired (@{watch.AccusedUsername})",
            _ => $"Rat Watch updated (@{watch.AccusedUsername})"
        };
    }

    private static DateTime GetRatWatchRelevantTimestamp(RatWatchDto watch)
    {
        // Return the most relevant timestamp based on status
        return watch.Status switch
        {
            Core.Enums.RatWatchStatus.Pending => watch.CreatedAt,
            Core.Enums.RatWatchStatus.Voting => watch.VotingStartedAt ?? watch.CreatedAt,
            Core.Enums.RatWatchStatus.Guilty => watch.VotingEndedAt ?? watch.VotingStartedAt ?? watch.CreatedAt,
            Core.Enums.RatWatchStatus.NotGuilty => watch.VotingEndedAt ?? watch.VotingStartedAt ?? watch.CreatedAt,
            Core.Enums.RatWatchStatus.ClearedEarly => watch.ClearedAt ?? watch.CreatedAt,
            Core.Enums.RatWatchStatus.Cancelled => watch.CreatedAt, // No specific cancelled timestamp
            Core.Enums.RatWatchStatus.Expired => watch.ScheduledAt, // Use scheduled time for expired
            _ => watch.CreatedAt
        };
    }

    private void BuildConnectedServersWidget(IEnumerable<GuildDto> guilds, IDictionary<ulong, int> commandCountsByGuild)
    {
        var guildList = guilds.ToList();

        // Gradient palette for avatar initials
        var gradients = new[]
        {
            "from-purple-500 to-pink-500",
            "from-blue-500 to-cyan-500",
            "from-orange-500 to-red-500",
            "from-green-500 to-emerald-500",
            "from-indigo-500 to-purple-500",
            "from-yellow-500 to-orange-500"
        };

        var serverItems = guildList.Select(guild =>
        {
            var commandsToday = commandCountsByGuild.TryGetValue(guild.Id, out var count) ? count : 0;

            // Determine status
            var status = ServerConnectionStatus.Offline;
            if (guild.IsActive)
            {
                status = commandsToday > 0 ? ServerConnectionStatus.Online : ServerConnectionStatus.Idle;
            }

            // Generate initials from guild name
            var initials = GenerateInitials(guild.Name);

            // Select gradient deterministically based on guild ID
            var gradientIndex = (int)(guild.Id % (uint)gradients.Length);
            var gradient = gradients[gradientIndex];

            return new ConnectedServerItemViewModel
            {
                Id = guild.Id,
                Name = guild.Name,
                IconUrl = guild.IconUrl,
                Initials = initials,
                AvatarGradient = gradient,
                MemberCount = guild.MemberCount ?? 0,
                Status = status,
                CommandsToday = commandsToday,
                DetailUrl = $"/Guilds/Details?id={guild.Id}"
            };
        })
        .OrderByDescending(s => s.CommandsToday)
        .ThenByDescending(s => s.MemberCount)
        .Take(5)
        .ToList();

        ConnectedServers = new ConnectedServersWidgetViewModel
        {
            Title = "Connected Servers",
            ViewAllUrl = "/Servers",
            Servers = serverItems,
            TotalServerCount = guildList.Count
        };

        _logger.LogDebug("Built Connected Servers widget with {DisplayedCount} of {TotalCount} servers",
            serverItems.Count, guildList.Count);
    }

    private static string GenerateInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "??";

        var words = name.Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length >= 2)
        {
            // Take first letter of first two words
            return $"{char.ToUpper(words[0][0])}{char.ToUpper(words[1][0])}";
        }
        else if (words.Length == 1 && words[0].Length >= 2)
        {
            // Take first two letters of single word
            return $"{char.ToUpper(words[0][0])}{char.ToUpper(words[0][1])}";
        }
        else if (words.Length == 1 && words[0].Length == 1)
        {
            // Single character, duplicate it
            return $"{char.ToUpper(words[0][0])}{char.ToUpper(words[0][0])}";
        }

        return "??";
    }

    /// <summary>
    /// Handler for restarting the bot (Admin only).
    /// </summary>
    public async Task<IActionResult> OnPostRestartBotAsync()
    {
        // Check admin authorization manually since [Authorize] can't be on handler methods
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to restart bot", User.Identity?.Name);
            return Forbid();
        }

        _logger.LogWarning("Bot restart requested by user {UserId}", User.Identity?.Name);

        try
        {
            await _botService.RestartAsync();
            _logger.LogInformation("Bot restart initiated successfully");

            // Return JSON response for AJAX
            return new JsonResult(new
            {
                success = true,
                message = "Bot is restarting..."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart bot");

            return new JsonResult(new
            {
                success = false,
                message = "Failed to restart bot. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }

    /// <summary>
    /// Handler for syncing all guilds.
    /// </summary>
    public async Task<IActionResult> OnPostSyncAllGuildsAsync()
    {
        _logger.LogInformation("Guild sync requested by user {UserId}", User.Identity?.Name);

        try
        {
            var syncedCount = await _guildService.SyncAllGuildsAsync();
            _logger.LogInformation("Successfully synced {SyncedCount} guilds", syncedCount);

            // Return JSON response for AJAX
            return new JsonResult(new
            {
                success = true,
                message = $"Successfully synced {syncedCount} guild{(syncedCount == 1 ? "" : "s")}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync guilds");

            return new JsonResult(new
            {
                success = false,
                message = "Failed to sync guilds. Please check logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }
}
