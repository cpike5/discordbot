using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using static DiscordBot.Core.Interfaces.GatewayConnectionState;

namespace DiscordBot.Bot.Hubs;

/// <summary>
/// SignalR hub for real-time dashboard updates.
/// Provides methods for guild-specific subscriptions, status retrieval, and alert notifications.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class DashboardHub : Hub
{
    /// <summary>
    /// The name of the SignalR group for alert notifications.
    /// </summary>
    public const string AlertsGroupName = "alerts";

    /// <summary>
    /// The name of the SignalR group for performance metrics updates.
    /// </summary>
    public const string PerformanceGroupName = "performance";

    /// <summary>
    /// The name of the SignalR group for system health updates.
    /// </summary>
    public const string SystemHealthGroupName = "system-health";

    /// <summary>
    /// Tracing attribute for SignalR connection ID.
    /// </summary>
    private const string SignalRConnectionIdAttribute = "signalr.connection.id";

    private readonly IBotService _botService;
    private readonly IConnectionStateService _connectionStateService;
    private readonly ILatencyHistoryService _latencyHistoryService;
    private readonly IPerformanceAlertService _alertService;
    private readonly ICommandPerformanceAggregator _commandPerformanceAggregator;
    private readonly IDatabaseMetricsCollector _databaseMetricsCollector;
    private readonly IBackgroundServiceHealthRegistry _backgroundServiceHealthRegistry;
    private readonly IInstrumentedCache _instrumentedCache;
    private readonly IPerformanceSubscriptionTracker _subscriptionTracker;
    private readonly ILogger<DashboardHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardHub"/> class.
    /// </summary>
    /// <param name="botService">The bot service for status retrieval.</param>
    /// <param name="connectionStateService">The connection state service.</param>
    /// <param name="latencyHistoryService">The latency history service.</param>
    /// <param name="alertService">The performance alert service.</param>
    /// <param name="commandPerformanceAggregator">The command performance aggregator service.</param>
    /// <param name="databaseMetricsCollector">The database metrics collector service.</param>
    /// <param name="backgroundServiceHealthRegistry">The background service health registry.</param>
    /// <param name="instrumentedCache">The instrumented cache service.</param>
    /// <param name="subscriptionTracker">The performance subscription tracker.</param>
    /// <param name="logger">The logger.</param>
    public DashboardHub(
        IBotService botService,
        IConnectionStateService connectionStateService,
        ILatencyHistoryService latencyHistoryService,
        IPerformanceAlertService alertService,
        ICommandPerformanceAggregator commandPerformanceAggregator,
        IDatabaseMetricsCollector databaseMetricsCollector,
        IBackgroundServiceHealthRegistry backgroundServiceHealthRegistry,
        IInstrumentedCache instrumentedCache,
        IPerformanceSubscriptionTracker subscriptionTracker,
        ILogger<DashboardHub> logger)
    {
        _botService = botService;
        _connectionStateService = connectionStateService;
        _latencyHistoryService = latencyHistoryService;
        _alertService = alertService;
        _commandPerformanceAggregator = commandPerformanceAggregator;
        _databaseMetricsCollector = databaseMetricsCollector;
        _backgroundServiceHealthRegistry = backgroundServiceHealthRegistry;
        _instrumentedCache = instrumentedCache;
        _subscriptionTracker = subscriptionTracker;
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "on_connected");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

        try
        {
            var userName = Context.User?.Identity?.Name ?? "unknown";

            _logger.LogInformation(
                "Dashboard client connected: ConnectionId={ConnectionId}, User={UserName}",
                Context.ConnectionId,
                userName);

            await base.OnConnectedAsync();

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnect, if any.</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "on_disconnected");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

        try
        {
            var userName = Context.User?.Identity?.Name ?? "unknown";

            // Clean up subscription tracking for this connection
            _subscriptionTracker.OnClientDisconnected(Context.ConnectionId);

            if (exception != null)
            {
                _logger.LogWarning(
                    exception,
                    "Dashboard client disconnected with error: ConnectionId={ConnectionId}, User={UserName}",
                    Context.ConnectionId,
                    userName);
            }
            else
            {
                _logger.LogInformation(
                    "Dashboard client disconnected: ConnectionId={ConnectionId}, User={UserName}",
                    Context.ConnectionId,
                    userName);
            }

            await base.OnDisconnectedAsync(exception);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Joins a guild-specific group to receive updates for that guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to subscribe to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinGuildGroup(ulong guildId)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "join_guild_group");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);
        activity?.SetTag(TracingConstants.Attributes.GuildId, guildId.ToString());

        try
        {
            var groupName = GetGuildGroupName(guildId);
            var userName = Context.User?.Identity?.Name ?? "unknown";

            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

            _logger.LogDebug(
                "Client joined guild group: ConnectionId={ConnectionId}, User={UserName}, GuildId={GuildId}",
                Context.ConnectionId,
                userName,
                guildId);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Leaves a guild-specific group to stop receiving updates for that guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to unsubscribe from.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeaveGuildGroup(ulong guildId)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "leave_guild_group");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);
        activity?.SetTag(TracingConstants.Attributes.GuildId, guildId.ToString());

        try
        {
            var groupName = GetGuildGroupName(guildId);
            var userName = Context.User?.Identity?.Name ?? "unknown";

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

            _logger.LogDebug(
                "Client left guild group: ConnectionId={ConnectionId}, User={UserName}, GuildId={GuildId}",
                Context.ConnectionId,
                userName,
                guildId);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the current bot status.
    /// </summary>
    /// <returns>The current bot status.</returns>
    public BotStatusDto GetCurrentStatus()
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "get_current_status");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

        try
        {
            _logger.LogDebug(
                "Status requested by client: ConnectionId={ConnectionId}",
                Context.ConnectionId);

            BotStatusDto result;
            using (BotActivitySource.StartServiceActivity("bot_service", "get_status"))
            {
                result = _botService.GetStatus();
            }

            BotActivitySource.SetSuccess(activity);
            return result;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the current health metrics including connection state, uptime, and latency.
    /// </summary>
    /// <returns>The current performance health status.</returns>
    public PerformanceHealthDto GetHealthStatus()
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "get_health_status");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

        try
        {
            _logger.LogDebug(
                "Health status requested by client: ConnectionId={ConnectionId}",
                Context.ConnectionId);

            GatewayConnectionState connectionState;
            using (BotActivitySource.StartServiceActivity("connection_state_service", "get_current_state"))
            {
                connectionState = _connectionStateService.GetCurrentState();
            }

            TimeSpan sessionDuration;
            using (BotActivitySource.StartServiceActivity("connection_state_service", "get_current_session_duration"))
            {
                sessionDuration = _connectionStateService.GetCurrentSessionDuration();
            }

            int currentLatency;
            using (BotActivitySource.StartServiceActivity("latency_history_service", "get_current_latency"))
            {
                currentLatency = _latencyHistoryService.GetCurrentLatency();
            }

            var health = new PerformanceHealthDto
            {
                Status = connectionState == GatewayConnectionState.Connected ? "Healthy" : "Unhealthy",
                Uptime = sessionDuration,
                LatencyMs = currentLatency,
                ConnectionState = connectionState.ToString(),
                Timestamp = DateTime.UtcNow
            };

            _logger.LogTrace(
                "Health status retrieved: Status={Status}, Uptime={Uptime}, Latency={LatencyMs}ms",
                health.Status,
                health.Uptime,
                health.LatencyMs);

            BotActivitySource.SetSuccess(activity);
            return health;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Joins the alerts group to receive real-time alert notifications.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinAlertsGroup()
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "join_alerts_group");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

        try
        {
            var userName = Context.User?.Identity?.Name ?? "unknown";

            await Groups.AddToGroupAsync(Context.ConnectionId, AlertsGroupName);

            _logger.LogDebug(
                "Client joined alerts group: ConnectionId={ConnectionId}, User={UserName}",
                Context.ConnectionId,
                userName);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Leaves the alerts group to stop receiving alert notifications.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeaveAlertsGroup()
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "leave_alerts_group");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

        try
        {
            var userName = Context.User?.Identity?.Name ?? "unknown";

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, AlertsGroupName);

            _logger.LogDebug(
                "Client left alerts group: ConnectionId={ConnectionId}, User={UserName}",
                Context.ConnectionId,
                userName);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the current active alert count for dashboard display.
    /// </summary>
    /// <returns>The active alert summary with counts by severity.</returns>
    public async Task<ActiveAlertSummaryDto> GetActiveAlertCount()
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "get_active_alert_count");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

        try
        {
            _logger.LogDebug(
                "Active alert count requested by client: ConnectionId={ConnectionId}",
                Context.ConnectionId);

            ActiveAlertSummaryDto summary;
            using (BotActivitySource.StartServiceActivity("alert_service", "get_active_alert_summary"))
            {
                summary = await _alertService.GetActiveAlertSummaryAsync();
            }

            _logger.LogTrace(
                "Active alert count retrieved: ActiveCount={ActiveCount}, Critical={CriticalCount}, Warning={WarningCount}",
                summary.ActiveCount,
                summary.CriticalCount,
                summary.WarningCount);

            BotActivitySource.SetSuccess(activity);
            return summary;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Joins the performance metrics group to receive real-time performance updates.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinPerformanceGroup()
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "join_performance_group");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

        try
        {
            var userName = Context.User?.Identity?.Name ?? "unknown";

            await Groups.AddToGroupAsync(Context.ConnectionId, PerformanceGroupName);

            // Track subscription for broadcast optimization
            _subscriptionTracker.OnJoinPerformanceGroup();
            _subscriptionTracker.TrackSubscription(Context.ConnectionId, PerformanceGroupName);

            _logger.LogDebug(
                "Client joined performance group: ConnectionId={ConnectionId}, User={UserName}",
                Context.ConnectionId,
                userName);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Leaves the performance metrics group to stop receiving performance updates.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeavePerformanceGroup()
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "leave_performance_group");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

        try
        {
            var userName = Context.User?.Identity?.Name ?? "unknown";

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, PerformanceGroupName);

            // Update subscription tracking
            _subscriptionTracker.OnLeavePerformanceGroup();
            _subscriptionTracker.UntrackSubscription(Context.ConnectionId, PerformanceGroupName);

            _logger.LogDebug(
                "Client left performance group: ConnectionId={ConnectionId}, User={UserName}",
                Context.ConnectionId,
                userName);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Joins the system health group to receive real-time system health updates.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinSystemHealthGroup()
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "join_system_health_group");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

        try
        {
            var userName = Context.User?.Identity?.Name ?? "unknown";

            await Groups.AddToGroupAsync(Context.ConnectionId, SystemHealthGroupName);

            // Track subscription for broadcast optimization
            _subscriptionTracker.OnJoinSystemHealthGroup();
            _subscriptionTracker.TrackSubscription(Context.ConnectionId, SystemHealthGroupName);

            _logger.LogDebug(
                "Client joined system health group: ConnectionId={ConnectionId}, User={UserName}",
                Context.ConnectionId,
                userName);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Leaves the system health group to stop receiving system health updates.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeaveSystemHealthGroup()
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "leave_system_health_group");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

        try
        {
            var userName = Context.User?.Identity?.Name ?? "unknown";

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, SystemHealthGroupName);

            // Update subscription tracking
            _subscriptionTracker.OnLeaveSystemHealthGroup();
            _subscriptionTracker.UntrackSubscription(Context.ConnectionId, SystemHealthGroupName);

            _logger.LogDebug(
                "Client left system health group: ConnectionId={ConnectionId}, User={UserName}",
                Context.ConnectionId,
                userName);

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the current performance metrics including latency, memory, CPU, and connection state.
    /// </summary>
    /// <returns>The current performance health metrics.</returns>
    public HealthMetricsUpdateDto GetCurrentPerformanceMetrics()
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "get_current_performance_metrics");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

        try
        {
            _logger.LogDebug(
                "Performance metrics requested by client: ConnectionId={ConnectionId}",
                Context.ConnectionId);

            int currentLatency;
            using (BotActivitySource.StartServiceActivity("latency_history_service", "get_current_latency"))
            {
                currentLatency = _latencyHistoryService.GetCurrentLatency();
            }

            GatewayConnectionState connectionState;
            using (BotActivitySource.StartServiceActivity("connection_state_service", "get_current_state"))
            {
                connectionState = _connectionStateService.GetCurrentState();
            }

            // Get current process metrics - dispose immediately to prevent memory leak
            long workingSetMB;
            long privateMemoryMB;
            int threadCount;
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                workingSetMB = process.WorkingSet64 / 1024 / 1024;
                privateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024;
                threadCount = process.Threads.Count;
            }

            var gen2Collections = GC.CollectionCount(2);

            // CPU usage calculation would require tracking over time, so we'll use 0 for now
            // This can be enhanced later with a proper CPU monitoring service
            var cpuUsagePercent = 0.0;

            var metrics = new HealthMetricsUpdateDto
            {
                LatencyMs = currentLatency,
                WorkingSetMB = workingSetMB,
                PrivateMemoryMB = privateMemoryMB,
                CpuUsagePercent = cpuUsagePercent,
                ThreadCount = threadCount,
                Gen2Collections = gen2Collections,
                ConnectionState = connectionState.ToString(),
                Timestamp = DateTime.UtcNow
            };

            _logger.LogTrace(
                "Performance metrics retrieved: Latency={LatencyMs}ms, WorkingSet={WorkingSetMB}MB, PrivateMemory={PrivateMemoryMB}MB, Threads={ThreadCount}",
                metrics.LatencyMs,
                metrics.WorkingSetMB,
                metrics.PrivateMemoryMB,
                metrics.ThreadCount);

            BotActivitySource.SetSuccess(activity);
            return metrics;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the current system health including database, cache, and background service metrics.
    /// </summary>
    /// <returns>The current system health metrics.</returns>
    public SystemMetricsUpdateDto GetCurrentSystemHealth()
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "get_current_system_health");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

        try
        {
            _logger.LogDebug(
                "System health requested by client: ConnectionId={ConnectionId}",
                Context.ConnectionId);

            DatabaseMetricsDto dbMetrics;
            using (BotActivitySource.StartServiceActivity("database_metrics_collector", "get_metrics"))
            {
                dbMetrics = _databaseMetricsCollector.GetMetrics();
            }

            IReadOnlyList<CacheStatisticsDto> cacheStats;
            using (BotActivitySource.StartServiceActivity("instrumented_cache", "get_statistics"))
            {
                cacheStats = _instrumentedCache.GetStatistics();
            }

            IReadOnlyList<BackgroundServiceHealthDto> serviceHealth;
            using (BotActivitySource.StartServiceActivity("background_service_health_registry", "get_all_health"))
            {
                serviceHealth = _backgroundServiceHealthRegistry.GetAllHealth();
            }

            // Calculate queries per second (simple approximation based on total queries)
            var queriesPerSecond = dbMetrics.TotalQueries > 0 ? dbMetrics.AvgQueryTimeMs > 0 ? 1000.0 / dbMetrics.AvgQueryTimeMs : 0 : 0;

            // Map cache statistics to dictionary by key prefix
            var cacheStatsDict = cacheStats.ToDictionary(
                c => c.KeyPrefix,
                c => new CacheStatsDto
                {
                    KeyPrefix = c.KeyPrefix,
                    Hits = c.Hits,
                    Misses = c.Misses,
                    HitRate = c.HitRate,
                    Size = c.Size
                });

            // Map background service health to simplified DTOs
            var serviceStatusList = serviceHealth.Select(s => new BackgroundServiceStatusDto
            {
                ServiceName = s.ServiceName,
                Status = s.Status,
                LastHeartbeat = s.LastHeartbeat,
                LastError = s.LastError
            }).ToList();

            var systemMetrics = new SystemMetricsUpdateDto
            {
                AvgQueryTimeMs = dbMetrics.AvgQueryTimeMs,
                TotalQueries = (int)dbMetrics.TotalQueries,
                QueriesPerSecond = queriesPerSecond,
                SlowQueryCount = dbMetrics.SlowQueryCount,
                CacheStats = cacheStatsDict,
                BackgroundServices = serviceStatusList,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogTrace(
                "System health retrieved: AvgQueryTime={AvgQueryTimeMs}ms, TotalQueries={TotalQueries}, SlowQueries={SlowQueryCount}, CacheCount={CacheCount}, ServicesCount={ServicesCount}",
                systemMetrics.AvgQueryTimeMs,
                systemMetrics.TotalQueries,
                systemMetrics.SlowQueryCount,
                systemMetrics.CacheStats.Count,
                systemMetrics.BackgroundServices.Count);

            BotActivitySource.SetSuccess(activity);
            return systemMetrics;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the current command performance metrics over a specified number of hours.
    /// </summary>
    /// <param name="hours">The number of hours of command history to aggregate (default: 24).</param>
    /// <returns>The current command performance metrics.</returns>
    public async Task<CommandPerformanceUpdateDto> GetCurrentCommandPerformance(int hours = 24)
    {
        using var activity = BotActivitySource.StartServiceActivity(
            "dashboard_hub",
            "get_current_command_performance");

        activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
        activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);
        activity?.SetTag("hours", hours);

        try
        {
            _logger.LogDebug(
                "Command performance requested by client: ConnectionId={ConnectionId}, Hours={Hours}",
                Context.ConnectionId,
                hours);

            IReadOnlyList<CommandPerformanceAggregateDto> aggregates;
            using (BotActivitySource.StartServiceActivity("command_performance_aggregator", "get_aggregates"))
            {
                aggregates = await _commandPerformanceAggregator.GetAggregatesAsync(hours);
            }

            // Calculate overall metrics from aggregates
            var totalCommands = aggregates.Sum(a => a.ExecutionCount);
            var avgResponseTimeMs = aggregates.Any() ? aggregates.Average(a => a.AvgMs) : 0;
            var p95ResponseTimeMs = aggregates.Any() ? aggregates.Average(a => a.P95Ms) : 0;
            var p99ResponseTimeMs = aggregates.Any() ? aggregates.Average(a => a.P99Ms) : 0;
            var errorRate = aggregates.Any() ? aggregates.Average(a => a.ErrorRate) : 0;

            // Calculate commands in the last hour (approximation: total / hours)
            var commandsLastHour = hours > 0 ? totalCommands / hours : totalCommands;

            var commandMetrics = new CommandPerformanceUpdateDto
            {
                TotalCommands24h = totalCommands,
                AvgResponseTimeMs = avgResponseTimeMs,
                P95ResponseTimeMs = p95ResponseTimeMs,
                P99ResponseTimeMs = p99ResponseTimeMs,
                ErrorRate = errorRate,
                CommandsLastHour = commandsLastHour,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogTrace(
                "Command performance retrieved: TotalCommands={TotalCommands}, AvgResponseTime={AvgResponseTimeMs}ms, P95={P95ResponseTimeMs}ms, ErrorRate={ErrorRate}%",
                commandMetrics.TotalCommands24h,
                commandMetrics.AvgResponseTimeMs,
                commandMetrics.P95ResponseTimeMs,
                commandMetrics.ErrorRate);

            BotActivitySource.SetSuccess(activity);
            return commandMetrics;
        }
        catch (Exception ex)
        {
            BotActivitySource.RecordException(activity, ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the group name for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The group name.</returns>
    private static string GetGuildGroupName(ulong guildId) => $"guild-{guildId}";
}
