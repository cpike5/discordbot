namespace DiscordBot.Core.Enums;

/// <summary>
/// Types of user notifications in the admin UI.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Alert about performance metrics exceeding thresholds.
    /// </summary>
    PerformanceAlert = 1,

    /// <summary>
    /// Notification about bot status changes (connected, disconnected, restarted).
    /// </summary>
    BotStatus = 2,

    /// <summary>
    /// Notification about guild events (joined, left, settings changed).
    /// </summary>
    GuildEvent = 3,

    /// <summary>
    /// Notification about command execution errors.
    /// </summary>
    CommandError = 4
}
