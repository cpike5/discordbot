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
    /// Tracing attribute for SignalR connection ID.
    /// </summary>
    private const string SignalRConnectionIdAttribute = "signalr.connection.id";

    private readonly IBotService _botService;
    private readonly IConnectionStateService _connectionStateService;
    private readonly ILatencyHistoryService _latencyHistoryService;
    private readonly IPerformanceAlertService _alertService;
    private readonly ILogger<DashboardHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardHub"/> class.
    /// </summary>
    /// <param name="botService">The bot service for status retrieval.</param>
    /// <param name="connectionStateService">The connection state service.</param>
    /// <param name="latencyHistoryService">The latency history service.</param>
    /// <param name="alertService">The performance alert service.</param>
    /// <param name="logger">The logger.</param>
    public DashboardHub(
        IBotService botService,
        IConnectionStateService connectionStateService,
        ILatencyHistoryService latencyHistoryService,
        IPerformanceAlertService alertService,
        ILogger<DashboardHub> logger)
    {
        _botService = botService;
        _connectionStateService = connectionStateService;
        _latencyHistoryService = latencyHistoryService;
        _alertService = alertService;
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
    /// Gets the group name for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The group name.</returns>
    private static string GetGuildGroupName(ulong guildId) => $"guild-{guildId}";
}
