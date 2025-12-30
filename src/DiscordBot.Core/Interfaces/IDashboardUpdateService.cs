using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service for broadcasting real-time updates to dashboard clients via SignalR.
/// Provides type-safe methods for each update type with proper error handling.
/// </summary>
public interface IDashboardUpdateService
{
    /// <summary>
    /// Broadcasts a bot status update to all connected dashboard clients.
    /// </summary>
    /// <param name="status">The bot status update data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastBotStatusAsync(BotStatusUpdateDto status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a command executed notification to all connected dashboard clients.
    /// </summary>
    /// <param name="update">The command execution update data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastCommandExecutedAsync(CommandExecutedUpdateDto update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a guild activity update to all connected dashboard clients.
    /// Also sends the update to the guild-specific group for targeted subscriptions.
    /// </summary>
    /// <param name="update">The guild activity update data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastGuildActivityAsync(GuildActivityUpdateDto update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts dashboard statistics update to all connected dashboard clients.
    /// </summary>
    /// <param name="stats">The dashboard statistics data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastStatsUpdateAsync(DashboardStatsDto stats, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a guild activity update to clients subscribed to a specific guild.
    /// </summary>
    /// <param name="guildId">The guild ID to target.</param>
    /// <param name="update">The guild activity update data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastGuildActivityToGuildAsync(ulong guildId, GuildActivityUpdateDto update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a Rat Watch activity event to all connected dashboard clients.
    /// This is a convenience method that creates a GuildActivityUpdateDto with Rat Watch-specific fields.
    /// </summary>
    /// <param name="guildId">The guild ID where the event occurred.</param>
    /// <param name="guildName">The guild name.</param>
    /// <param name="eventType">Type of Rat Watch event (RatWatchCreated, RatWatchVotingStarted, etc.).</param>
    /// <param name="username">The username involved in the event.</param>
    /// <param name="details">Optional additional details (e.g., verdict result).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BroadcastRatWatchActivityAsync(
        ulong guildId,
        string guildName,
        string eventType,
        string username,
        string? details = null,
        CancellationToken cancellationToken = default);
}
