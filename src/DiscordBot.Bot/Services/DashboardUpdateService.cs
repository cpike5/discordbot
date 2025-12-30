using DiscordBot.Bot.Hubs;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for broadcasting real-time updates to dashboard clients via SignalR.
/// Provides type-safe broadcast methods with error handling that doesn't impact callers.
/// </summary>
public class DashboardUpdateService : IDashboardUpdateService
{
    private readonly IHubContext<DashboardHub> _hubContext;
    private readonly ILogger<DashboardUpdateService> _logger;

    // SignalR event names - keep consistent with client-side handlers
    private const string BotStatusUpdatedEvent = "BotStatusUpdated";
    private const string CommandExecutedEvent = "CommandExecuted";
    private const string GuildActivityEvent = "GuildActivity";
    private const string StatsUpdatedEvent = "StatsUpdated";

    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardUpdateService"/> class.
    /// </summary>
    /// <param name="hubContext">The SignalR hub context for DashboardHub.</param>
    /// <param name="logger">The logger instance.</param>
    public DashboardUpdateService(
        IHubContext<DashboardHub> hubContext,
        ILogger<DashboardUpdateService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task BroadcastBotStatusAsync(BotStatusUpdateDto status, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Broadcasting bot status update: ConnectionState={ConnectionState}, GuildCount={GuildCount}",
                status.ConnectionState,
                status.GuildCount);

            await _hubContext.Clients.All.SendAsync(
                BotStatusUpdatedEvent,
                status,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to broadcast bot status update: ConnectionState={ConnectionState}",
                status.ConnectionState);
        }
    }

    /// <inheritdoc/>
    public async Task BroadcastCommandExecutedAsync(CommandExecutedUpdateDto update, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Broadcasting command executed: Command={CommandName}, GuildId={GuildId}, Success={Success}",
                update.CommandName,
                update.GuildId,
                update.Success);

            await _hubContext.Clients.All.SendAsync(
                CommandExecutedEvent,
                update,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to broadcast command executed: Command={CommandName}",
                update.CommandName);
        }
    }

    /// <inheritdoc/>
    public async Task BroadcastGuildActivityAsync(GuildActivityUpdateDto update, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Broadcasting guild activity: GuildId={GuildId}, EventType={EventType}",
                update.GuildId,
                update.EventType);

            // Broadcast to all clients
            await _hubContext.Clients.All.SendAsync(
                GuildActivityEvent,
                update,
                cancellationToken);

            // Also send to guild-specific group for targeted subscriptions
            var groupName = GetGuildGroupName(update.GuildId);
            await _hubContext.Clients.Group(groupName).SendAsync(
                GuildActivityEvent,
                update,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to broadcast guild activity: GuildId={GuildId}, EventType={EventType}",
                update.GuildId,
                update.EventType);
        }
    }

    /// <inheritdoc/>
    public async Task BroadcastStatsUpdateAsync(DashboardStatsDto stats, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug(
                "Broadcasting stats update: CommandsToday={CommandsToday}, TotalMembers={TotalMembers}",
                stats.CommandsToday,
                stats.TotalMembers);

            await _hubContext.Clients.All.SendAsync(
                StatsUpdatedEvent,
                stats,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to broadcast stats update");
        }
    }

    /// <inheritdoc/>
    public async Task BroadcastGuildActivityToGuildAsync(ulong guildId, GuildActivityUpdateDto update, CancellationToken cancellationToken = default)
    {
        try
        {
            var groupName = GetGuildGroupName(guildId);

            _logger.LogDebug(
                "Broadcasting guild activity to guild group: GuildId={GuildId}, EventType={EventType}",
                guildId,
                update.EventType);

            await _hubContext.Clients.Group(groupName).SendAsync(
                GuildActivityEvent,
                update,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to broadcast guild activity to guild: GuildId={GuildId}, EventType={EventType}",
                guildId,
                update.EventType);
        }
    }

    /// <inheritdoc/>
    public async Task BroadcastRatWatchActivityAsync(
        ulong guildId,
        string guildName,
        string eventType,
        string username,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var update = new GuildActivityUpdateDto
        {
            GuildId = guildId,
            GuildName = guildName,
            EventType = eventType,
            Username = username,
            Details = details,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogDebug(
            "Broadcasting Rat Watch activity: GuildId={GuildId}, EventType={EventType}, Username={Username}",
            guildId,
            eventType,
            username);

        await BroadcastGuildActivityAsync(update, cancellationToken);
    }

    /// <summary>
    /// Gets the SignalR group name for a guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <returns>The group name string.</returns>
    private static string GetGuildGroupName(ulong guildId) => $"guild-{guildId}";
}
