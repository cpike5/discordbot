using DiscordBot.Bot.Hubs;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for sending real-time notifications to dashboard clients via SignalR.
/// </summary>
public class DashboardNotifier : IDashboardNotifier
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<DashboardNotifier> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardNotifier"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context.</param>
    /// <param name="logger">The logger.</param>
    public DashboardNotifier(
        IHubContext<DashboardHub> hubContext,
        ILogger<DashboardNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task BroadcastBotStatusAsync(BotStatusDto status, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting bot status update to all clients");

        await _hubContext.Clients.All.SendAsync(
            "BotStatusUpdated",
            status,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendGuildUpdateAsync(
        ulong guildId,
        string eventName,
        object data,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"guild-{guildId}";

        _logger.LogDebug(
            "Sending guild update: GuildId={GuildId}, Event={EventName}",
            guildId,
            eventName);

        await _hubContext.Clients.Group(groupName).SendAsync(
            eventName,
            data,
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task BroadcastToAllAsync(
        string eventName,
        object data,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Broadcasting to all clients: Event={EventName}", eventName);

        await _hubContext.Clients.All.SendAsync(
            eventName,
            data,
            cancellationToken);
    }
}
