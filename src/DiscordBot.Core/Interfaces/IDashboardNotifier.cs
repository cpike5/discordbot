using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Interface for sending real-time notifications to dashboard clients.
/// Used by services to broadcast updates through SignalR.
/// </summary>
public interface IDashboardNotifier
{
    /// <summary>
    /// Broadcasts a bot status update to all connected clients.
    /// </summary>
    /// <param name="status">The updated bot status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastBotStatusAsync(BotStatusDto status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a guild-specific update to clients subscribed to that guild.
    /// </summary>
    /// <param name="guildId">The guild ID.</param>
    /// <param name="eventName">The event name for client-side handling.</param>
    /// <param name="data">The event data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SendGuildUpdateAsync(ulong guildId, string eventName, object data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a message to all connected dashboard clients.
    /// </summary>
    /// <param name="eventName">The event name for client-side handling.</param>
    /// <param name="data">The event data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastToAllAsync(string eventName, object data, CancellationToken cancellationToken = default);
}
