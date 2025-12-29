namespace DiscordBot.Bot.ViewModels.Components.Enums;

/// <summary>
/// Represents the connection status of a Discord server.
/// </summary>
public enum ServerConnectionStatus
{
    /// <summary>
    /// Guild is active and bot is connected with recent command activity.
    /// </summary>
    Online,

    /// <summary>
    /// Guild is active but has no recent command activity.
    /// </summary>
    Idle,

    /// <summary>
    /// Guild is in database but bot is not currently connected or guild is inactive.
    /// </summary>
    Offline
}
