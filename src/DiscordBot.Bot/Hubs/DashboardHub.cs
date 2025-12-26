using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DiscordBot.Bot.Hubs;

/// <summary>
/// SignalR hub for real-time dashboard updates.
/// Provides methods for guild-specific subscriptions and status retrieval.
/// </summary>
[Authorize(Policy = "RequireViewer")]
public class DashboardHub : Hub
{
    private readonly IBotService _botService;
    private readonly ILogger<DashboardHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardHub"/> class.
    /// </summary>
    /// <param name="botService">The bot service for status retrieval.</param>
    /// <param name="logger">The logger.</param>
    public DashboardHub(
        IBotService botService,
        ILogger<DashboardHub> logger)
    {
        _botService = botService;
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
    /// Gets the group name for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The group name.</returns>
    private static string GetGuildGroupName(ulong guildId) => $"guild-{guildId}";
}
