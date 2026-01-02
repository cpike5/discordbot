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
        var userName = Context.User?.Identity?.Name ?? "unknown";

        _logger.LogInformation(
            "Dashboard client connected: ConnectionId={ConnectionId}, User={UserName}",
            Context.ConnectionId,
            userName);

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnect, if any.</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
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
    }

    /// <summary>
    /// Joins a guild-specific group to receive updates for that guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to subscribe to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinGuildGroup(ulong guildId)
    {
        var groupName = GetGuildGroupName(guildId);
        var userName = Context.User?.Identity?.Name ?? "unknown";

        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug(
            "Client joined guild group: ConnectionId={ConnectionId}, User={UserName}, GuildId={GuildId}",
            Context.ConnectionId,
            userName,
            guildId);
    }

    /// <summary>
    /// Leaves a guild-specific group to stop receiving updates for that guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to unsubscribe from.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeaveGuildGroup(ulong guildId)
    {
        var groupName = GetGuildGroupName(guildId);
        var userName = Context.User?.Identity?.Name ?? "unknown";

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        _logger.LogDebug(
            "Client left guild group: ConnectionId={ConnectionId}, User={UserName}, GuildId={GuildId}",
            Context.ConnectionId,
            userName,
            guildId);
    }

    /// <summary>
    /// Gets the current bot status.
    /// </summary>
    /// <returns>The current bot status.</returns>
    public BotStatusDto GetCurrentStatus()
    {
        _logger.LogDebug(
            "Status requested by client: ConnectionId={ConnectionId}",
            Context.ConnectionId);

        return _botService.GetStatus();
    }

    /// <summary>
    /// Gets the current health metrics including connection state, uptime, and latency.
    /// </summary>
    /// <returns>The current performance health status.</returns>
    public PerformanceHealthDto GetHealthStatus()
    {
        _logger.LogDebug(
            "Health status requested by client: ConnectionId={ConnectionId}",
            Context.ConnectionId);

        var connectionState = _connectionStateService.GetCurrentState();
        var sessionDuration = _connectionStateService.GetCurrentSessionDuration();
        var currentLatency = _latencyHistoryService.GetCurrentLatency();

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

        return health;
    }

    /// <summary>
    /// Joins the alerts group to receive real-time alert notifications.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinAlertsGroup()
    {
        var userName = Context.User?.Identity?.Name ?? "unknown";

        await Groups.AddToGroupAsync(Context.ConnectionId, AlertsGroupName);

        _logger.LogDebug(
            "Client joined alerts group: ConnectionId={ConnectionId}, User={UserName}",
            Context.ConnectionId,
            userName);
    }

    /// <summary>
    /// Leaves the alerts group to stop receiving alert notifications.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeaveAlertsGroup()
    {
        var userName = Context.User?.Identity?.Name ?? "unknown";

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AlertsGroupName);

        _logger.LogDebug(
            "Client left alerts group: ConnectionId={ConnectionId}, User={UserName}",
            Context.ConnectionId,
            userName);
    }

    /// <summary>
    /// Gets the current active alert count for dashboard display.
    /// </summary>
    /// <returns>The active alert summary with counts by severity.</returns>
    public async Task<ActiveAlertSummaryDto> GetActiveAlertCount()
    {
        _logger.LogDebug(
            "Active alert count requested by client: ConnectionId={ConnectionId}",
            Context.ConnectionId);

        var summary = await _alertService.GetActiveAlertSummaryAsync();

        _logger.LogTrace(
            "Active alert count retrieved: ActiveCount={ActiveCount}, Critical={CriticalCount}, Warning={WarningCount}",
            summary.ActiveCount,
            summary.CriticalCount,
            summary.WarningCount);

        return summary;
    }

    /// <summary>
    /// Gets the group name for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The group name.</returns>
    private static string GetGuildGroupName(ulong guildId) => $"guild-{guildId}";
}
