namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for controlling which events generate admin notifications.
/// </summary>
public class NotificationOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Notification";

    /// <summary>
    /// Gets or sets whether to create notifications for performance alerts (Critical/Warning).
    /// Default is true.
    /// </summary>
    public bool EnablePerformanceAlerts { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to create notifications for bot status changes (connected/disconnected).
    /// Default is true.
    /// </summary>
    public bool EnableBotStatusChanges { get; set; } = true;

    /// <summary>
    /// Gets or sets how long (in seconds) the gateway must stay disconnected before a
    /// "Bot Disconnected" notification is created. Discord routinely asks connected bots to
    /// reconnect (gateway opcode 7, node rotation), and Discord.NET resumes within a second or two;
    /// those blips are not worth a notification. A "Bot Connected" notification is only sent when
    /// the matching disconnect was reported. Set to 0 to notify on every disconnect.
    /// Default is 60 seconds.
    /// </summary>
    public int BotDisconnectGraceSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets whether to create notifications for guild events (joined/left).
    /// Default is true.
    /// </summary>
    public bool EnableGuildEvents { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to create notifications for command errors (unhandled exceptions).
    /// Default is true.
    /// </summary>
    public bool EnableCommandErrors { get; set; } = true;

    /// <summary>
    /// Gets or sets the time window (in minutes) for duplicate notification suppression.
    /// If a notification with the same type, entity type, and entity ID exists within this window,
    /// a new notification will not be created.
    /// Default is 5 minutes.
    /// </summary>
    public int DuplicateSuppressionMinutes { get; set; } = 5;
}
