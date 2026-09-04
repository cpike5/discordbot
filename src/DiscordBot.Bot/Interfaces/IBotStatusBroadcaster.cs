namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Publishes bot connection status to dashboard clients and drives Discord presence
/// (rich-presence status text). Extracted from <see cref="Services.BotHostedService"/> so
/// status/presence broadcasting is independent of gateway login/logout lifecycle.
/// </summary>
public interface IBotStatusBroadcaster
{
    /// <summary>
    /// Registers the custom-status source and subscribes to the events (settings changes,
    /// Rat Watch updates) that should trigger a status refresh. Call once at startup.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Unregisters the custom-status source and unsubscribes from refresh-trigger events.
    /// Call during shutdown.
    /// </summary>
    void Shutdown();

    /// <summary>
    /// Broadcasts the bot's current connection status (state, latency, guild count, uptime)
    /// to dashboard clients. Fire-and-forget with internal error handling.
    /// </summary>
    Task BroadcastStatusAsync();

    /// <summary>
    /// Evaluates all registered status sources and applies the highest-priority active one.
    /// Intended to be called once the gateway connects.
    /// </summary>
    Task ApplyStartupStatusAsync();
}
