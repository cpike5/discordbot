using System.Security.Claims;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DiscordBot.Bot.Hubs;

/// <summary>
/// SignalR hub for real-time dashboard updates.
/// Provides methods for guild-specific subscriptions, status retrieval, and alert notifications.
/// Group/connection lifecycle lives here; the actual data for each feature area is delegated to a
/// per-feature service (<see cref="IDashboardMetricsService"/>, <see cref="IDashboardAudioStatusService"/>,
/// <see cref="IDashboardNotificationQueryService"/>) so this hub stays a thin transport shim.
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
    /// The prefix for guild-specific audio groups.
    /// Full group name is "{AudioGroupPrefix}{guildId}".
    /// </summary>
    public const string AudioGroupPrefix = "guild-audio-";

    /// <summary>
    /// The name of the SignalR group for bulk purge progress updates.
    /// </summary>
    public const string BulkPurgeGroupName = "bulk-purge";

    // Notification event names
    /// <summary>
    /// Event name for when a new notification is received by a user.
    /// </summary>
    public const string OnNotificationReceived = "OnNotificationReceived";

    /// <summary>
    /// Event name for when the notification count changes for a user.
    /// </summary>
    public const string OnNotificationCountChanged = "OnNotificationCountChanged";

    /// <summary>
    /// Event name for when a single notification is marked as read.
    /// </summary>
    public const string OnNotificationMarkedRead = "OnNotificationMarkedRead";

    /// <summary>
    /// Event name for when all notifications are marked as read.
    /// </summary>
    public const string OnAllNotificationsRead = "OnAllNotificationsRead";

    /// <summary>
    /// Tracing attribute for SignalR connection ID.
    /// </summary>
    private const string SignalRConnectionIdAttribute = "signalr.connection.id";

    private readonly IDashboardMetricsService _metricsService;
    private readonly IDashboardAudioStatusService _audioStatusService;
    private readonly IDashboardNotificationQueryService _notificationQueryService;
    private readonly IPerformanceSubscriptionTracker _subscriptionTracker;
    private readonly ILogger<DashboardHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardHub"/> class.
    /// </summary>
    /// <param name="metricsService">The service providing bot status, health, alert, and performance metrics.</param>
    /// <param name="audioStatusService">The service providing guild audio/voice connection status.</param>
    /// <param name="notificationQueryService">The service providing per-user notification operations.</param>
    /// <param name="subscriptionTracker">The performance subscription tracker.</param>
    /// <param name="logger">The logger.</param>
    public DashboardHub(
        IDashboardMetricsService metricsService,
        IDashboardAudioStatusService audioStatusService,
        IDashboardNotificationQueryService notificationQueryService,
        IPerformanceSubscriptionTracker subscriptionTracker,
        ILogger<DashboardHub> logger)
    {
        _metricsService = metricsService;
        _audioStatusService = audioStatusService;
        _notificationQueryService = notificationQueryService;
        _subscriptionTracker = subscriptionTracker;
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "on_connected",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

                var userName = Context.User?.Identity?.Name ?? "unknown";

                _logger.LogInformation(
                    "Dashboard client connected: ConnectionId={ConnectionId}, User={UserName}",
                    Context.ConnectionId,
                    userName);

                await base.OnConnectedAsync();
            });
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnect, if any.</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "on_disconnected",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

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
            });
    }

    /// <summary>
    /// Joins a guild-specific group to receive updates for that guild.
    /// </summary>
    /// <param name="guildIdString">The Discord guild ID as a string (to preserve precision from JavaScript).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinGuildGroup(string guildIdString)
    {
        if (!ulong.TryParse(guildIdString, out var guildId))
        {
            _logger.LogWarning("Invalid guild ID format received: {GuildIdString}", guildIdString);
            throw new ArgumentException("Invalid guild ID format", nameof(guildIdString));
        }

        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "join_guild_group",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);
                activity?.SetTag(TracingConstants.Attributes.GuildId, guildId.ToString());

                var groupName = GetGuildGroupName(guildId);
                var userName = Context.User?.Identity?.Name ?? "unknown";

                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

                _logger.LogDebug(
                    "Client joined guild group: ConnectionId={ConnectionId}, User={UserName}, GuildId={GuildId}",
                    Context.ConnectionId,
                    userName,
                    guildId);
            });
    }

    /// <summary>
    /// Leaves a guild-specific group to stop receiving updates for that guild.
    /// </summary>
    /// <param name="guildIdString">The Discord guild ID as a string (to preserve precision from JavaScript).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeaveGuildGroup(string guildIdString)
    {
        if (!ulong.TryParse(guildIdString, out var guildId))
        {
            _logger.LogWarning("Invalid guild ID format received: {GuildIdString}", guildIdString);
            throw new ArgumentException("Invalid guild ID format", nameof(guildIdString));
        }

        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "leave_guild_group",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);
                activity?.SetTag(TracingConstants.Attributes.GuildId, guildId.ToString());

                var groupName = GetGuildGroupName(guildId);
                var userName = Context.User?.Identity?.Name ?? "unknown";

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

                _logger.LogDebug(
                    "Client left guild group: ConnectionId={ConnectionId}, User={UserName}, GuildId={GuildId}",
                    Context.ConnectionId,
                    userName,
                    guildId);
            });
    }

    /// <summary>
    /// Gets the current bot status.
    /// </summary>
    /// <returns>The current bot status.</returns>
    public BotStatusDto GetCurrentStatus()
        => _metricsService.GetCurrentStatus(Context.ConnectionId, Context.User?.Identity?.Name);

    /// <summary>
    /// Gets the current health metrics including connection state, uptime, and latency.
    /// </summary>
    /// <returns>The current performance health status.</returns>
    public PerformanceHealthDto GetHealthStatus()
        => _metricsService.GetHealthStatus(Context.ConnectionId, Context.User?.Identity?.Name);

    /// <summary>
    /// Joins the alerts group to receive real-time alert notifications.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinAlertsGroup()
    {
        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "join_alerts_group",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

                var userName = Context.User?.Identity?.Name ?? "unknown";

                await Groups.AddToGroupAsync(Context.ConnectionId, AlertsGroupName);

                _logger.LogDebug(
                    "Client joined alerts group: ConnectionId={ConnectionId}, User={UserName}",
                    Context.ConnectionId,
                    userName);
            });
    }

    /// <summary>
    /// Leaves the alerts group to stop receiving alert notifications.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeaveAlertsGroup()
    {
        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "leave_alerts_group",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

                var userName = Context.User?.Identity?.Name ?? "unknown";

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, AlertsGroupName);

                _logger.LogDebug(
                    "Client left alerts group: ConnectionId={ConnectionId}, User={UserName}",
                    Context.ConnectionId,
                    userName);
            });
    }

    /// <summary>
    /// Joins the bulk purge group to receive progress updates during bulk purge operations.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinBulkPurgeGroup()
    {
        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "join_bulk_purge_group",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

                var userName = Context.User?.Identity?.Name ?? "unknown";

                await Groups.AddToGroupAsync(Context.ConnectionId, BulkPurgeGroupName);

                _logger.LogDebug(
                    "Client joined bulk purge group: ConnectionId={ConnectionId}, User={UserName}",
                    Context.ConnectionId,
                    userName);
            });
    }

    /// <summary>
    /// Leaves the bulk purge group to stop receiving progress updates.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeaveBulkPurgeGroup()
    {
        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "leave_bulk_purge_group",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

                var userName = Context.User?.Identity?.Name ?? "unknown";

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, BulkPurgeGroupName);

                _logger.LogDebug(
                    "Client left bulk purge group: ConnectionId={ConnectionId}, User={UserName}",
                    Context.ConnectionId,
                    userName);
            });
    }

    /// <summary>
    /// Gets the current active alert count for dashboard display.
    /// </summary>
    /// <returns>The active alert summary with counts by severity.</returns>
    public Task<ActiveAlertSummaryDto> GetActiveAlertCount()
        => _metricsService.GetActiveAlertCountAsync(Context.ConnectionId, Context.User?.Identity?.Name);

    /// <summary>
    /// Joins the performance metrics group to receive real-time performance updates.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinPerformanceGroup()
    {
        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "join_performance_group",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

                var userName = Context.User?.Identity?.Name ?? "unknown";

                await Groups.AddToGroupAsync(Context.ConnectionId, PerformanceGroupName);

                // Track subscription for broadcast optimization
                _subscriptionTracker.OnJoinPerformanceGroup();
                _subscriptionTracker.TrackSubscription(Context.ConnectionId, PerformanceGroupName);

                _logger.LogDebug(
                    "Client joined performance group: ConnectionId={ConnectionId}, User={UserName}",
                    Context.ConnectionId,
                    userName);
            });
    }

    /// <summary>
    /// Leaves the performance metrics group to stop receiving performance updates.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeavePerformanceGroup()
    {
        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "leave_performance_group",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

                var userName = Context.User?.Identity?.Name ?? "unknown";

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, PerformanceGroupName);

                // Update subscription tracking
                _subscriptionTracker.OnLeavePerformanceGroup();
                _subscriptionTracker.UntrackSubscription(Context.ConnectionId, PerformanceGroupName);

                _logger.LogDebug(
                    "Client left performance group: ConnectionId={ConnectionId}, User={UserName}",
                    Context.ConnectionId,
                    userName);
            });
    }

    /// <summary>
    /// Joins the system health group to receive real-time system health updates.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinSystemHealthGroup()
    {
        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "join_system_health_group",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

                var userName = Context.User?.Identity?.Name ?? "unknown";

                await Groups.AddToGroupAsync(Context.ConnectionId, SystemHealthGroupName);

                // Track subscription for broadcast optimization
                _subscriptionTracker.OnJoinSystemHealthGroup();
                _subscriptionTracker.TrackSubscription(Context.ConnectionId, SystemHealthGroupName);

                _logger.LogDebug(
                    "Client joined system health group: ConnectionId={ConnectionId}, User={UserName}",
                    Context.ConnectionId,
                    userName);
            });
    }

    /// <summary>
    /// Leaves the system health group to stop receiving system health updates.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeaveSystemHealthGroup()
    {
        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "leave_system_health_group",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);

                var userName = Context.User?.Identity?.Name ?? "unknown";

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, SystemHealthGroupName);

                // Update subscription tracking
                _subscriptionTracker.OnLeaveSystemHealthGroup();
                _subscriptionTracker.UntrackSubscription(Context.ConnectionId, SystemHealthGroupName);

                _logger.LogDebug(
                    "Client left system health group: ConnectionId={ConnectionId}, User={UserName}",
                    Context.ConnectionId,
                    userName);
            });
    }

    /// <summary>
    /// Gets the current performance metrics including latency, memory, CPU, and connection state.
    /// </summary>
    /// <returns>The current performance health metrics.</returns>
    public HealthMetricsUpdateDto GetCurrentPerformanceMetrics()
        => _metricsService.GetCurrentPerformanceMetrics(Context.ConnectionId, Context.User?.Identity?.Name);

    /// <summary>
    /// Gets the current system health including database, cache, and background service metrics.
    /// </summary>
    /// <returns>The current system health metrics.</returns>
    public SystemMetricsUpdateDto GetCurrentSystemHealth()
        => _metricsService.GetCurrentSystemHealth(Context.ConnectionId, Context.User?.Identity?.Name);

    /// <summary>
    /// Gets the current command performance metrics over a specified number of hours.
    /// </summary>
    /// <param name="hours">The number of hours of command history to aggregate (default: 24).</param>
    /// <returns>The current command performance metrics.</returns>
    public Task<CommandPerformanceUpdateDto> GetCurrentCommandPerformance(int hours = 24)
        => _metricsService.GetCurrentCommandPerformanceAsync(Context.ConnectionId, Context.User?.Identity?.Name, hours);

    /// <summary>
    /// Joins a guild-specific audio group to receive audio events for that guild.
    /// </summary>
    /// <param name="guildIdString">The Discord guild ID as a string (to preserve precision from JavaScript).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task JoinGuildAudioGroup(string guildIdString)
    {
        if (!ulong.TryParse(guildIdString, out var guildId))
        {
            _logger.LogWarning("Invalid guild ID format received: {GuildIdString}", guildIdString);
            throw new ArgumentException("Invalid guild ID format", nameof(guildIdString));
        }

        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "join_guild_audio_group",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);
                activity?.SetTag(TracingConstants.Attributes.GuildId, guildId.ToString());

                var groupName = GetGuildAudioGroupName(guildId);
                var userName = Context.User?.Identity?.Name ?? "unknown";

                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

                _logger.LogDebug(
                    "Client joined guild audio group: ConnectionId={ConnectionId}, User={UserName}, GuildId={GuildId}",
                    Context.ConnectionId,
                    userName,
                    guildId);
            });
    }

    /// <summary>
    /// Leaves a guild-specific audio group to stop receiving audio events for that guild.
    /// </summary>
    /// <param name="guildIdString">The Discord guild ID as a string (to preserve precision from JavaScript).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LeaveGuildAudioGroup(string guildIdString)
    {
        if (!ulong.TryParse(guildIdString, out var guildId))
        {
            _logger.LogWarning("Invalid guild ID format received: {GuildIdString}", guildIdString);
            throw new ArgumentException("Invalid guild ID format", nameof(guildIdString));
        }

        await ServiceActivityHelper.ExecuteAsync(
            "dashboard_hub", "leave_guild_audio_group",
            async activity =>
            {
                activity?.SetTag(TracingConstants.Attributes.UserId, Context.User?.Identity?.Name);
                activity?.SetTag(SignalRConnectionIdAttribute, Context.ConnectionId);
                activity?.SetTag(TracingConstants.Attributes.GuildId, guildId.ToString());

                var groupName = GetGuildAudioGroupName(guildId);
                var userName = Context.User?.Identity?.Name ?? "unknown";

                await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

                _logger.LogDebug(
                    "Client left guild audio group: ConnectionId={ConnectionId}, User={UserName}, GuildId={GuildId}",
                    Context.ConnectionId,
                    userName,
                    guildId);
            });
    }

    /// <summary>
    /// Gets the current audio status for a guild.
    /// </summary>
    /// <param name="guildIdString">The Discord guild ID as a string (to preserve precision from JavaScript).</param>
    /// <returns>The current audio status for the guild.</returns>
    public AudioStatusDto GetCurrentAudioStatus(string guildIdString)
    {
        if (!ulong.TryParse(guildIdString, out var guildId))
        {
            _logger.LogWarning("Invalid guild ID format received: {GuildIdString}", guildIdString);
            throw new ArgumentException("Invalid guild ID format", nameof(guildIdString));
        }

        return _audioStatusService.GetCurrentAudioStatus(guildId, Context.ConnectionId, Context.User?.Identity?.Name);
    }

    // ============================================================================
    // Notification Methods
    // ============================================================================

    /// <summary>
    /// Gets the notification summary (unread count by type) for the current user.
    /// </summary>
    /// <returns>The notification summary for the current user, or an empty summary if user is not authenticated.</returns>
    public async Task<NotificationSummaryDto> GetNotificationSummary()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null)
        {
            _logger.LogDebug("GetNotificationSummary called with no authenticated user");
            return new NotificationSummaryDto();
        }

        return await _notificationQueryService.GetNotificationSummaryAsync(userId, Context.ConnectionId);
    }

    /// <summary>
    /// Gets recent notifications for the current user.
    /// </summary>
    /// <param name="limit">Maximum number of notifications to return (default: 15, max: 100).</param>
    /// <returns>The notifications for the current user, or empty if user is not authenticated.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when limit is negative or exceeds 100.</exception>
    public async Task<IEnumerable<UserNotificationDto>> GetNotifications(int limit = 15)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, 100);

        var userId = GetAuthenticatedUserId();
        if (userId == null)
        {
            _logger.LogDebug("GetNotifications called with no authenticated user");
            return [];
        }

        return await _notificationQueryService.GetNotificationsAsync(userId, Context.ConnectionId, limit);
    }

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    /// <param name="notificationId">The notification ID to mark as read.</param>
    public async Task MarkNotificationRead(Guid notificationId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null)
        {
            _logger.LogDebug("MarkNotificationRead called with no authenticated user");
            return;
        }

        await _notificationQueryService.MarkNotificationReadAsync(userId, Context.ConnectionId, notificationId);
    }

    /// <summary>
    /// Marks all notifications as read for the current user.
    /// </summary>
    public async Task MarkAllNotificationsRead()
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null)
        {
            _logger.LogDebug("MarkAllNotificationsRead called with no authenticated user");
            return;
        }

        await _notificationQueryService.MarkAllNotificationsReadAsync(userId, Context.ConnectionId);
    }

    /// <summary>
    /// Dismisses (soft deletes) a notification.
    /// </summary>
    /// <param name="notificationId">The notification ID to dismiss.</param>
    public async Task DismissNotification(Guid notificationId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId == null)
        {
            _logger.LogDebug("DismissNotification called with no authenticated user");
            return;
        }

        await _notificationQueryService.DismissNotificationAsync(userId, Context.ConnectionId, notificationId);
    }

    // ============================================================================
    // Private Helpers
    // ============================================================================

    /// <summary>
    /// Gets the authenticated user ID from the SignalR context.
    /// </summary>
    /// <returns>The user ID if authenticated, null otherwise.</returns>
    private string? GetAuthenticatedUserId()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrEmpty(userId) ? null : userId;
    }

    /// <summary>
    /// Gets the group name for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The group name.</returns>
    private static string GetGuildGroupName(ulong guildId) => $"guild-{guildId}";

    /// <summary>
    /// Gets the audio group name for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The audio group name.</returns>
    internal static string GetGuildAudioGroupName(ulong guildId) => $"{AudioGroupPrefix}{guildId}";
}
