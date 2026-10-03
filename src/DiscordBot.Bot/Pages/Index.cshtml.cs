using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Dashboard;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Components.Enums;
using static DiscordBot.Bot.ViewModels.Components.Enums.ServerConnectionStatus;

namespace DiscordBot.Bot.Pages;

/// <summary>
/// Dashboard page for authenticated users.
/// Anonymous users are redirected to the public landing page via middleware in Program.cs.
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
    private readonly IDashboardStatsProvider _statsProvider;
    private readonly IDashboardStatsBroadcaster _statsBroadcaster;

    public QuickActionsCardViewModel QuickActions { get; private set; } = default!;
    public AuditLogCardViewModel? AuditLog { get; private set; }

    // Dashboard Redesign ViewModels
    public BotStatusBannerViewModel BotStatusBanner { get; private set; } = default!;
    public List<HeroMetricCardViewModel> HeroMetrics { get; private set; } = new();
    public ActivityFeedTimelineViewModel ActivityTimeline { get; private set; } = default!;
    public ConnectedServersWidgetViewModel ConnectedServers { get; private set; } = default!;

    /// <summary>
    /// Whether the viewer may see the servers list. A Viewer cannot open a server's page, so the
    /// card is left out for them (D8) and the grid closes up around it.
    /// </summary>
    public bool ShowConnectedServers { get; private set; }

    /// <summary>Whether the viewer may see the audit log card (Admin and above).</summary>
    public bool ShowAuditLog { get; private set; }

    public IndexModel(
        ILogger<IndexModel> logger,
        IBotService botService,
        IGuildService guildService,
        ICommandLogService commandLogService,
        IAuditLogService auditLogService,
        IVersionService versionService,
        IRatWatchService ratWatchService,
        IConnectionStateService connectionStateService,
        IDashboardStatsProvider statsProvider,
        IDashboardStatsBroadcaster statsBroadcaster)
    {
        _logger = logger;
        _botService = botService;
        _guildService = guildService;
        _commandLogService = commandLogService;
        _auditLogService = auditLogService;
        _versionService = versionService;
        _ratWatchService = ratWatchService;
        _connectionStateService = connectionStateService;
        _statsProvider = statsProvider;
        _statsBroadcaster = statsBroadcaster;
    }

    private bool IsAdmin => User.IsInRole("Admin") || User.IsInRole("SuperAdmin");

    private bool IsModerator => IsAdmin || User.IsInRole("Moderator");

    public async Task<IActionResult> OnGetAsync()
    {
        // Authorization is handled by [Authorize(Policy = "RequireViewer")] attribute
        // Anonymous users are redirected to /landing by middleware before authorization runs
        _logger.LogDebug("Dashboard accessed by authenticated user {UserId}", User.Identity?.Name);

        var statusDto = _botService.GetStatus();

        _logger.LogTrace("Bot status retrieved: {ConnectionState}, Latency: {LatencyMs}ms, Guilds: {GuildCount}",
            statusDto.ConnectionState, statusDto.LatencyMs, statusDto.GuildCount);

        // Determine admin status for conditional audit log fetch
        var isAdmin = IsAdmin;
        ShowConnectedServers = IsModerator;
        ShowAuditLog = isAdmin;

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

        var totalCommands = commandStats.Values.Sum();
        _logger.LogDebug("Dashboard data retrieved: {GuildCount} guilds, {TotalCommands} commands, {ActivityCount} recent logs",
            guilds.Count, totalCommands, recentLogsResponse.Items.Count);

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
                    Label = "Sync All Servers",
                    IconPath = "M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15",
                    Color = QuickActionColor.Blue,
                    ActionType = QuickActionType.PostAction,
                    Handler = "SyncAllGuilds",
                    RequiresConfirmation = true,
                    ConfirmationModalId = "syncGuildsModal",
                    IsAdminOnly = true
                },
                new()
                {
                    Id = "settings",
                    Label = "Settings",
                    IconPath = "M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z M15 12a3 3 0 11-6 0 3 3 0 016 0z",
                    Color = QuickActionColor.Gray,
                    ActionType = QuickActionType.Link,
                    Href = "/Admin/Settings",
                    IsAdminOnly = true
                }
            }
        };

        // Build Dashboard Redesign ViewModels
        var stats = DashboardStatsProvider.Build(
            guilds,
            totalCommands,
            _connectionStateService.GetUptimePercentage(DashboardStatsProvider.Window));
        BuildBotStatusBanner(statusDto, stats);
        BuildHeroMetrics(stats);
        BuildActivityTimeline(recentLogsResponse.Items, ratWatchActivity);
        BuildConnectedServersWidget(guilds, commandCountsByGuild);

        return Page();
    }


    private void BuildBotStatusBanner(BotStatusDto statusDto, DashboardStatsDto stats)
    {
        BotStatusBanner = new BotStatusBannerViewModel
        {
            IsOnline = statusDto.ConnectionState == "Connected",
            StatusText = statusDto.ConnectionState == "Connected" ? "Connected" : "Disconnected",
            ServerCount = stats.TotalServers,
            TotalMembers = stats.TotalMembers,
            UptimeDisplay = BotStatusViewModel.FormatUptime(statusDto.Uptime),
            Version = _versionService.GetVersion(),
            LatencyMs = statusDto.LatencyMs
        };
    }

    /// <summary>
    /// The hero cards. Each value carries a <c>data-stat-*</c> attribute that
    /// <c>dashboard-realtime.js</c> rewrites from the <c>StatsUpdated</c> event; the names line up
    /// with <see cref="DashboardStatsDto"/>.
    /// </summary>
    private void BuildHeroMetrics(DashboardStatsDto stats)
    {
        var uptimeDisplay = DisplayFormat.Number(stats.UptimePercent24Hours, 1) + "%";

        // SVG icon paths matching the prototype design
        const string serverIcon = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"2\" d=\"M5 12h14M5 12a2 2 0 01-2-2V6a2 2 0 012-2h14a2 2 0 012 2v4a2 2 0 01-2 2M5 12a2 2 0 00-2 2v4a2 2 0 002 2h14a2 2 0 002-2v-4a2 2 0 00-2-2m-2-4h.01M17 16h.01\" />";
        const string usersIcon = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"2\" d=\"M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z\" />";
        const string commandIcon = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"2\" d=\"M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z\" />";
        const string uptimeIcon = "<path stroke-linecap=\"round\" stroke-linejoin=\"round\" stroke-width=\"2\" d=\"M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z\" />";

        // Titles say what is counted: the command count is a rolling 24 hours, and "members" is the
        // sum of each server's member count (not distinct users).
        HeroMetrics = new List<HeroMetricCardViewModel>
        {
            new() { Title = "Servers", Value = DisplayFormat.Number(stats.TotalServers), DataAttribute = "data-stat-servers", AccentColor = CardAccent.Blue, IconSvg = serverIcon, ShowSparkline = false },
            new() { Title = "Members", Value = DisplayFormat.Number(stats.TotalMembers), DataAttribute = "data-stat-members", AccentColor = CardAccent.Success, IconSvg = usersIcon, ShowSparkline = false },
            new() { Title = "Commands (24h)", Value = DisplayFormat.Number(stats.CommandsLast24Hours), DataAttribute = "data-stat-commands", AccentColor = CardAccent.Orange, IconSvg = commandIcon, ShowSparkline = false },
            new() { Title = "Uptime (24h)", Value = uptimeDisplay, DataAttribute = "data-stat-uptime", AccentColor = CardAccent.Info, IconSvg = uptimeIcon, ShowSparkline = false }
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

        ActivityTimeline = new ActivityFeedTimelineViewModel { Title = "Recent Activity", Items = items, ShowRefreshButton = true, ViewAllUrl = Url.Page("/Commands/Index", new { tab = "logs" }) ?? "/Commands", MaxHeight = "400px" };
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

        // Avatar fills for initials: token classes (white text reads on every fill), whole names so
        // Tailwind finds them
        var avatarClasses = new[]
        {
            "bg-accent-orange",
            "bg-accent-blue",
            "bg-success",
            "bg-info",
            "bg-accent-purple"
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

            // Same server, same colour, every render
            var avatarClass = avatarClasses[(int)(guild.Id % (uint)avatarClasses.Length)];

            return new ConnectedServerItemViewModel
            {
                Id = guild.Id,
                Name = guild.Name,
                IconUrl = guild.IconUrl,
                Initials = initials,
                AvatarClass = avatarClass,
                MemberCount = guild.MemberCount ?? 0,
                Status = status,
                CommandsToday = commandsToday,
                DetailUrl = Url.Page("/Guilds/Details", new { guildId = guild.Id }) ?? "/Guilds"
            };
        })
        .OrderByDescending(s => s.CommandsToday)
        .ThenByDescending(s => s.MemberCount)
        .Take(5)
        .ToList();

        ConnectedServers = new ConnectedServersWidgetViewModel
        {
            Title = "Connected Servers",
            ViewAllUrl = Url.Page("/Guilds/Index") ?? "/Guilds",
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
        if (words.Length == 0)
            return "??";

        // Text elements, not chars: a name that starts with an emoji keeps it whole (UX plan E-1).
        var initials = words.Length >= 2
            ? TextDisplay.Take(words[0], 1) + TextDisplay.Take(words[1], 1)
            : TextDisplay.Take(words[0], 2);

        // A single character is shown twice, as before.
        if (words.Length == 1 && initials == TextDisplay.Take(words[0], 1))
            initials += initials;

        return initials.ToUpperInvariant();
    }

    /// <summary>
    /// The hero numbers as JSON. Dashboards ask for them again after a hub reconnect (pushes sent
    /// while the connection was down are gone) and after Sync All.
    /// </summary>
    public async Task<IActionResult> OnGetStatsAsync(CancellationToken cancellationToken)
    {
        return new JsonResult(await _statsProvider.GetStatsAsync(cancellationToken));
    }

    /// <summary>
    /// The Connected Servers rows as JSON, for the dashboard to redraw after a sync. Ids are
    /// strings: a snowflake does not survive a trip through a JavaScript number.
    /// </summary>
    public async Task<IActionResult> OnGetConnectedServersAsync()
    {
        if (!IsModerator)
        {
            return Forbid();
        }

        var guilds = await _guildService.GetAllGuildsAsync();
        var commandCountsByGuild = await _commandLogService.GetCommandCountsByGuildAsync(DateTime.UtcNow.Date);
        BuildConnectedServersWidget(guilds, commandCountsByGuild);

        return new JsonResult(new
        {
            totalServerCount = ConnectedServers.TotalServerCount,
            servers = ConnectedServers.Servers.Select(s => new
            {
                id = s.Id.ToString(),
                name = s.Name,
                iconUrl = s.IconUrl,
                initials = s.Initials,
                avatarClass = s.AvatarClass,
                memberCount = s.MemberCount,
                status = s.Status.ToString(),
                commandsToday = s.CommandsToday,
                detailUrl = s.DetailUrl
            })
        });
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
                message = "The bot restarted and is reconnecting to Discord."
            });
        }
        catch (NotSupportedException ex)
        {
            // Offline mode: there is no gateway connection to restart
            _logger.LogInformation(ex, "Bot restart refused: {Reason}", ex.Message);

            return new JsonResult(new
            {
                success = false,
                message = "The bot is running in offline mode and has no Discord connection to restart."
            })
            {
                StatusCode = StatusCodes.Status409Conflict
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart bot");

            return new JsonResult(new
            {
                success = false,
                message = "Could not restart the bot. Check the logs for details."
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
        // The page only requires Viewer; syncing every guild is an admin action
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin"))
        {
            _logger.LogWarning("Non-admin user {UserId} attempted to sync all guilds", User.Identity?.Name);
            return new JsonResult(new
            {
                success = false,
                message = "You do not have permission to sync servers."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        _logger.LogInformation("Guild sync requested by user {UserId}", User.Identity?.Name);

        try
        {
            var syncedCount = await _guildService.SyncAllGuildsAsync();
            _logger.LogInformation("Successfully synced {SyncedCount} guilds", syncedCount);

            // Open dashboards (this one included) pick up the new server and member counts
            _statsBroadcaster.NotifyChanged();

            // Return JSON response for AJAX
            return new JsonResult(new
            {
                success = true,
                message = syncedCount == 0
                    ? "The bot is not connected to any servers, so there was nothing to sync."
                    : $"Synced {DisplayFormat.Plural(syncedCount, "server")} from Discord."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync guilds");

            return new JsonResult(new
            {
                success = false,
                message = "Could not sync servers. Check the logs for details."
            })
            {
                StatusCode = 500
            };
        }
    }
}
