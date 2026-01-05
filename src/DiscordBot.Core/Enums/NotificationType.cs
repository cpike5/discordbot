namespace DiscordBot.Core.Enums;

/// <summary>
/// Types of notifications that can be sent to users in the admin UI.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Alert triggered by performance threshold breach.
    /// Examples: High latency, memory pressure, error rate spike.
    /// </summary>
    PerformanceAlert = 1,

    /// <summary>
    /// Bot status change notification.
    /// Examples: Bot connected, disconnected, reconnecting.
    /// </summary>
    BotStatus = 2,

    /// <summary>
    /// Guild-related event notification.
    /// Examples: Bot joined/left guild, member milestone reached.
    /// </summary>
    GuildEvent = 3,

    /// <summary>
    /// Command execution error notification.
    /// Examples: Unhandled exception, rate limit hit, permission denied.
    /// </summary>
    CommandError = 4
}
